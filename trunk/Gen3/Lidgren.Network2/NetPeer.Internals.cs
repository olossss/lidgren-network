using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;

namespace Lidgren.Network2
{
	//
	// Everything in this partial file is run on the network thread and these methods should not be callable by the application directly
	//
	public partial class NetPeer
	{
		private int m_runSleepInMilliseconds = 1;
		private byte[] m_receiveBuffer;
		internal byte[] m_sendBuffer;
		private EndPoint m_senderRemote;
		internal Socket m_socket;
		private Queue<NetIncomingMessage> m_releasedIncomingMessages;
		internal byte[] m_macAddressBytes;

		private Queue<NetOutgoingMessage> m_unsentUnconnectedMessage;
		private Queue<IPEndPoint> m_unsentUnconnectedRecipients;

		private void InternalStart()
		{
			m_releasedIncomingMessages.Clear();
			m_unsentUnconnectedMessage.Clear();
			m_unsentUnconnectedRecipients.Clear();
		}

		/// <summary>
		/// Gets or sets the amount of time in milliseconds the network thread should sleep; recommended values 1 or 0
		/// </summary>
		public int NetworkThreadSleepTime
		{
			get { return m_runSleepInMilliseconds; }
			set { m_runSleepInMilliseconds = value; }
		}

		//
		// Network loop
		//
		private void Run()
		{
			//
			// Initialize
			//
			VerifyNetworkThread();

			System.Net.NetworkInformation.PhysicalAddress pa = NetUtility.GetMacAddress();
			if (pa != null)
			{
				m_macAddressBytes = pa.GetAddressBytes();
				LogVerbose("Mac address is " + NetUtility.ToHexString(m_macAddressBytes));
			}
			else
			{
				LogWarning("Failed to get Mac address");
			}

			InitializeRecycling();

			LogDebug("Network thread started");

			lock (m_initializeLock)
			{
				if (m_isInitialized)
					return;
				m_configuration.Lock();

				m_statistics.Reset();

				// bind to socket
				IPEndPoint iep = null;
				try
				{
					iep = new IPEndPoint(m_configuration.LocalAddress, m_configuration.Port);
					EndPoint ep = (EndPoint)iep;

					m_socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
					m_socket.ReceiveBufferSize = m_configuration.ReceiveBufferSize;
					m_socket.SendBufferSize = m_configuration.SendBufferSize;
					m_socket.Blocking = false;
					m_socket.Bind(ep);

					IPEndPoint boundEp = m_socket.LocalEndPoint as IPEndPoint;
					LogDebug("Socket bound to " + boundEp + ": " + m_socket.IsBound);

					m_receiveBuffer = new byte[m_configuration.ReceiveBufferSize];
					m_sendBuffer = new byte[m_configuration.SendBufferSize];

					LogVerbose("Initialization done");

					// only set initialized if everything succeeds
					m_isInitialized = true;
				}
				catch (SocketException sex)
				{
					if (sex.SocketErrorCode == SocketError.AddressAlreadyInUse)
						throw new NetException("Failed to bind to port " + (iep == null ? "Null" : iep.ToString()) + " - Address already in use!", sex);
					throw;
				}
				catch (Exception ex)
				{
					throw new NetException("Failed to bind to " + (iep == null ? "Null" : iep.ToString()), ex);
				}
			}

			//
			// Network loop
			//

			do
			{
				try
				{
					Heartbeat();
				}
				catch (Exception ex)
				{
					LogWarning(ex.ToString());
				}

				// wait here to give cpu to other threads/processes; also to collect messages for aggregate packet
				Thread.Sleep(m_runSleepInMilliseconds);
			} while (!m_initiateShutdown);

			//
			// perform shutdown
			//
			LogDebug("Shutting down...");

			// disconnect and make one final heartbeat
			foreach (NetConnection conn in m_connections)
				conn.ExecuteDisconnect(NetMessagePriority.High);

			// one final heartbeat, to get the disconnects onto the wire
			Heartbeat();

			lock (m_initializeLock)
			{
				// TODO: Send all outgoing message immediately

				try
				{
					if (m_socket != null)
					{
						m_socket.Shutdown(SocketShutdown.Receive);
						m_socket.Close(2); // 2 seconds timeout
					}
				}
				finally
				{
					m_socket = null;
					m_isInitialized = false;
					m_initiateShutdown = false;
					LogDebug("Shutdown complete");
				}
			}

			//
			// TODO: make sure everything is DE-initialized (release etc)
			//

			return;
		}

		internal void ReleaseMessage(NetIncomingMessage msg)
		{
			lock (m_releasedIncomingMessages)
				m_releasedIncomingMessages.Enqueue(msg);
		}

		private void Heartbeat()
		{
			VerifyNetworkThread();

			// do connection heartbeats
			foreach (NetConnection conn in m_connections)
				conn.Heartbeat();

			// send unconnected sends
			if (m_unsentUnconnectedMessage.Count > 0)
			{
				lock (m_unsentUnconnectedMessage)
				{
					while (m_unsentUnconnectedMessage.Count > 0)
					{
						NetOutgoingMessage msg = m_unsentUnconnectedMessage.Dequeue();
						IPEndPoint recipient = m_unsentUnconnectedRecipients.Dequeue();

						int msgPayloadLength = msg.LengthBytes;

						// no sequence number
						m_sendBuffer[0] = 0;
						m_sendBuffer[1] = 0;

						int ptr = 2;
						if (msgPayloadLength < 256)
						{
							m_sendBuffer[ptr++] = (byte)((int)msg.m_type << 2);
							m_sendBuffer[ptr++] = (byte)msgPayloadLength;
						}
						else
						{
							m_sendBuffer[ptr++] = (byte)(((int)msg.m_type << 2) | 1);
							m_sendBuffer[ptr++] = (byte)(msgPayloadLength & 255);
							m_sendBuffer[ptr++] = (byte)((msgPayloadLength << 8) & 255);
						}

						if (msgPayloadLength > 0)
							Buffer.BlockCopy(msg.m_data, 0, m_sendBuffer, ptr, msgPayloadLength);
						ptr += msgPayloadLength;

						if (recipient.Address.Equals(IPAddress.Broadcast))
						{
							// send using broadcast
							try
							{
								m_socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
								SendPacket(ptr, recipient);
							}
							finally
							{
								m_socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, false);
							}
						}
						else
						{
							// send normally
							SendPacket(ptr, recipient);
						}
					}
				}
			}

			//
			// read from socket
			//
			while (true)
			{
				if (m_socket == null || m_socket.Available < 1)
					return;

				int bytesReceived = 0;
				try
				{
					bytesReceived = m_socket.ReceiveFrom(m_receiveBuffer, 0, m_receiveBuffer.Length, SocketFlags.None, ref m_senderRemote);
					if (bytesReceived >= 0)
						m_statistics.m_receivedBytes += bytesReceived;
				}
				catch (SocketException sx)
				{
					// no good response to this yet
					if (sx.ErrorCode == 10054)
					{
						// connection reset by peer, aka forcibly closed
						// we should shut down the connection; but m_senderRemote seemingly cannot be trusted, so which connection should we shut down?!
						LogWarning("Connection reset by peer, seemingly from " + m_senderRemote);
						return;
					}

					LogWarning(sx.ToString());
					return;
				}

				if (bytesReceived < 1)
					return;

				m_statistics.m_receivedPackets++;

				double now = NetTime.Now;

				//LogVerbose("Received " + bytesReceived + " bytes");

				// TODO: add received bytes statistics

				IPEndPoint ipsender = (IPEndPoint)m_senderRemote;

				NetConnection sender = null;
				m_connectionLookup.TryGetValue(ipsender, out sender);

				if (sender != null)
				{
					Debug.Assert(ipsender.Equals(sender.m_remoteEndPoint));
					sender.m_lastHeardFrom = now;
				}

				if (bytesReceived < 2)
				{
					// malformed packet
					LogWarning("Malformed packet from " + sender + " (" + ipsender + "); only " + bytesReceived + " bytes long");
					continue;
				}

				//
				// parse packet into messages
				//
				{
					ushort packetNumber = (ushort)(m_receiveBuffer[0] | (m_receiveBuffer[1] << 8));

					LogVerbose("Received packet R#" + packetNumber + " (" + bytesReceived + " bytes)");

					if (packetNumber != 0 && sender != null)
						sender.SendAcknowledge(packetNumber);

					int ptr = 2;
					while (ptr < bytesReceived)
					{
						// get message flags
						byte a = m_receiveBuffer[ptr++];
						bool isLargePayload = ((a & 1) == 1);
						NetMessageType mtp = (NetMessageType)(a >> 2);

						if (ptr >= bytesReceived)
						{
							LogWarning("Malformed message; not enough bytes");
							break;
						}

						// get payload length
						int payloadLength;
						if (isLargePayload)
						{
							payloadLength = m_receiveBuffer[ptr] | m_receiveBuffer[ptr + 1] << 8;
							ptr += 2;
						}
						else
						{
							payloadLength = m_receiveBuffer[ptr++];
						}

						// get payload
						byte[] payload = GetStorage(payloadLength);
						Buffer.BlockCopy(m_receiveBuffer, ptr, payload, 0, payloadLength);
						ptr += payloadLength;

						//
						// handle incoming message
						//

						if (mtp == NetMessageType.Error)
						{
							LogError("Malformed message; no message type!");
							continue;
						}

						// reject messages requiring a connection
						if (sender == null)
						{
							// handle unconnected message
							HandleUnconnectedMessage(mtp, payload, payloadLength, ipsender);
						}
						else
						{
							// handle connected message
							sender.HandleReceivedConnectedMessage(now, mtp, payload, payloadLength);
						}
					}
				}
			}
			// heartbeat done
		}

		private void HandleUnconnectedMessage(NetMessageType mtp, byte[] payload, int payloadLength, IPEndPoint senderEndPoint)
		{
			VerifyNetworkThread();

			switch (mtp)
			{
				case NetMessageType.LibraryConnect:
					if (!m_configuration.m_acceptIncomingConnections)
					{
						LogWarning("Connect received; but we're not accepting incoming connections!");
						return;
					}

					// ok, someone wants to connect to us, and we're accepting connections!
					if (m_connections.Count >= m_configuration.MaximumConnections)
					{
						HandleServerFull(senderEndPoint);
						return;
					}

					// TODO: connection approval, including hail data

					NetConnection conn = new NetConnection(this, senderEndPoint);
					conn.m_connectionInitiator = false;
					lock (m_connections)
					{
						m_connections.Add(conn);
						m_connectionLookup[senderEndPoint] = conn;
					}
					conn.SetStatus(NetConnectionStatus.Connecting, "Connecting");

					// send connection response
					LogVerbose("Sending LibraryConnectResponse");
					NetOutgoingMessage reply = CreateMessage(2);
					reply.m_type = NetMessageType.LibraryConnectResponse;
					conn.EnqueueOutgoingMessage(reply, NetMessagePriority.High);

					conn.m_connectInitationTime = NetTime.Now;

					break;
				case NetMessageType.LibraryAckNack:
				case NetMessageType.LibraryAcknowledge:
				case NetMessageType.LibraryConnectionEstablished:
				case NetMessageType.LibraryConnectionRejected:
				case NetMessageType.LibraryConnectResponse:
				case NetMessageType.LibraryDisconnect:
					throw new NetException("NetMessageType not valid for unconnected source");

				case NetMessageType.LibraryDiscovery:
				case NetMessageType.LibraryDiscoveryResponse:
				case NetMessageType.LibraryNatIntroduction:
					throw new NotImplementedException();

				case NetMessageType.LibraryKeepAlive:
					// no operation - we just want the the packet ack
					break;
				default:
					// user data
					if (m_configuration.IsMessageTypeEnabled(NetIncomingMessageType.UnconnectedData))
					{
						NetIncomingMessage ium = CreateIncomingMessage(NetIncomingMessageType.UnconnectedData, payload, payloadLength);
						ium.m_senderEndPoint = senderEndPoint;
						ReleaseMessage(ium);
					}
					break;
			}
		}

		private void HandleServerFull(IPEndPoint connecter)
		{
			const string rejectMessage = "Server is full!";

			NetOutgoingMessage reply = CreateMessage(rejectMessage.Length + 1);
			reply.m_type = NetMessageType.LibraryConnectionRejected;
			reply.Write(rejectMessage);
			EnqueueUnconnectedMessage(reply, connecter);
		}

		private void EnqueueUnconnectedMessage(NetOutgoingMessage msg, IPEndPoint recipient)
		{
			msg.m_type = NetMessageType.UserUnreliable; // sortof not applicable
			Interlocked.Increment(ref msg.m_inQueueCount);
			lock (m_unsentUnconnectedMessage)
			{
				m_unsentUnconnectedMessage.Enqueue(msg);
				m_unsentUnconnectedRecipients.Enqueue(recipient);
			}
		}
	}
}