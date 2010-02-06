using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;

namespace Lidgren.Network
{
	//
	// Everything in this partial file is run on the network thread and these methods should not be callable by the application directly
	//
	public partial class NetPeer
	{
		internal const int PACKET_HEADER_SIZE = 6;
		internal const int WINDOW_SIZE = 32;
		internal const int NUM_SERIALS = 256;
		
		private int m_runSleepInMilliseconds = 1;
		private byte[] m_receiveBuffer;
		internal byte[] m_sendBuffer;
		private EndPoint m_senderRemote;
		internal Socket m_socket;
		private Queue<NetIncomingMessage> m_releasedIncomingMessages;
		internal byte[] m_macAddressBytes;
		private int m_listenPort;

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

		/// <summary>
		/// Gets the port number this NetPeer is listening and sending on
		/// </summary>
		public int Port { get { return m_listenPort; } }

		/// <summary>
		/// Gets a semi-unique identifier based on Mac address and ip/port. Note! Not available until Start has been called!
		/// </summary>
		public int UniqueIdentifier { get { return m_uniqueIdentifier; } }

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
				if (m_status == NetPeerStatus.Running)
					return;

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

					m_listenPort = boundEp.Port;

					m_uniqueIdentifier = NetUtility.GetMacAddress().GetHashCode() ^ boundEp.GetHashCode();

					m_receiveBuffer = new byte[m_configuration.ReceiveBufferSize];
					m_sendBuffer = new byte[m_configuration.SendBufferSize];

					LogVerbose("Initialization done");

					// only set Running if everything succeeds
					m_status = NetPeerStatus.Running;
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
			} while (m_status == NetPeerStatus.Running);

			//
			// perform shutdown
			//
			LogDebug("Shutting down...");

			// disconnect and make one final heartbeat
			foreach (NetConnection conn in m_connections)
			{
				conn.m_disconnectByeMessage = "Shutting down";
				conn.m_disconnectRequested = true;
			}

			// one final heartbeat, will send stuff and do disconnect
			Heartbeat();

			lock (m_initializeLock)
			{
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
					m_status = NetPeerStatus.NotRunning;
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

#if DEBUG
			// send delayed packets
			SendDelayedPackets();
#endif

		// do connection heartbeats
		RestartConnectionHeartbeat:
			foreach (NetConnection conn in m_connections)
			{
				conn.Heartbeat();
				if (conn.m_status == NetConnectionStatus.Disconnected)
				{
					RemoveConnection(conn);
					goto RestartConnectionHeartbeat;
				}
			}

			// send unconnected sends
			if (m_unsentUnconnectedMessage.Count > 0)
			{
				lock (m_unsentUnconnectedMessage)
				{
					while (m_unsentUnconnectedMessage.Count > 0)
					{
						m_sendBuffer[0] = 0; // serial
						m_sendBuffer[1] = 0; // ack serial

						m_sendBuffer[2] = 0; // ack mask
						m_sendBuffer[3] = 0; // ack mask
						m_sendBuffer[4] = 0; // ack mask
						m_sendBuffer[5] = 0; // ack mask
		
						NetOutgoingMessage msg = m_unsentUnconnectedMessage.Dequeue();
						IPEndPoint recipient = m_unsentUnconnectedRecipients.Dequeue();

						int msgPayloadLength = msg.LengthBytes;

						int ptr = NetPeer.PACKET_HEADER_SIZE;

						m_sendBuffer[ptr++] = (byte)msg.m_type;

						System.Diagnostics.Debug.Assert(msgPayloadLength < 32768);
						if (msgPayloadLength < 127)
						{
							m_sendBuffer[ptr++] = (byte)msgPayloadLength;
						}
						else
						{
							m_sendBuffer[ptr++] = (byte)((msgPayloadLength & 127) | 128);
							m_sendBuffer[ptr++] = (byte)(msgPayloadLength >> 7);
						}

						if (msgPayloadLength > 0)
						{
							Buffer.BlockCopy(msg.m_data, 0, m_sendBuffer, ptr, msgPayloadLength);
							ptr += msgPayloadLength;
						}
						
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
				m_statistics.m_receivedBytes += bytesReceived;

				double now = NetTime.Now;

				//LogVerbose("Received " + bytesReceived + " bytes");

				IPEndPoint ipsender = (IPEndPoint)m_senderRemote;

				NetConnection sender = null;
				m_connectionLookup.TryGetValue(ipsender, out sender);


				if (sender != null)
				{
					Debug.Assert(ipsender.Equals(sender.m_remoteEndPoint));
					sender.m_lastHeardFrom = now;
				}

				if (bytesReceived < PACKET_HEADER_SIZE)
				{
					// malformed packet
					LogWarning("Malformed packet from " + sender + " (" + ipsender + "); only " + bytesReceived + " bytes long");
					continue;
				}

				bool useMessages = (sender == null ? true : sender.ReceivePacket(now, m_receiveBuffer, bytesReceived));

				//
				// parse packet into messages
				//
				if (useMessages)
				{
					int ptr = PACKET_HEADER_SIZE;
					while (ptr < bytesReceived)
					{
						// get message flags
						NetMessageType mtp = (NetMessageType)m_receiveBuffer[ptr++];

						if (ptr >= bytesReceived)
						{
							LogWarning("Malformed message from " + ipsender.ToString() + "; not enough bytes");
							break;
						}

						int payloadLength = (int)m_receiveBuffer[ptr++];
						if ((payloadLength & 128) == 128) // large payload
							payloadLength = (payloadLength & 127) | (m_receiveBuffer[ptr++] << 7);

						if ((ptr + payloadLength) > bytesReceived)
						{
							LogWarning("Malformed message from " + ipsender.ToString() + "; not enough bytes");
							break;
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

					string appIdent;
					string hail;
					try
					{
						NetIncomingMessage reader = new NetIncomingMessage();
						reader.m_data = payload;
						reader.m_bitLength = payloadLength * 8;
						appIdent = reader.ReadString();
						hail = reader.ReadString();
					}
					catch (Exception ex)
					{
						// malformed connect packet
						LogWarning("Malformed connect packet from " + senderEndPoint + " - " + ex.ToString());
						return;
					}

					if (appIdent.Equals(m_configuration.AppIdentifier) == false)
					{
						// wrong app ident
						LogWarning("Connect received with wrong appidentifier (need '" + m_configuration.AppIdentifier + "' found '" + appIdent + "') from " + senderEndPoint);
						return;
					}

					// ok, someone wants to connect to us, and we're accepting connections!
					if (m_connections.Count >= m_configuration.MaximumConnections)
					{
						HandleServerFull(senderEndPoint);
						return;
					}

					// TODO: connection approval, pass hail data

					NetConnection conn = new NetConnection(this, senderEndPoint);
					conn.m_connectionInitiator = false;
					conn.m_receiveWindowBase = 1;
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
				case NetMessageType.LibraryConnectionEstablished:
				case NetMessageType.LibraryConnectResponse:
					throw new NetException("NetMessageType not valid for unconnected source");

				case NetMessageType.LibraryDisconnect:
					// really should never happen; but we'll let this one slip
					break;

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

		internal void RemoveConnection(NetConnection conn)
		{
			conn.Dispose();
			m_connections.Remove(conn);
			m_connectionLookup.Remove(conn.m_remoteEndPoint);
		}

		private void HandleServerFull(IPEndPoint connecter)
		{
			const string rejectMessage = "Server is full!";

			NetOutgoingMessage reply = CreateMessage(rejectMessage.Length + 1);
			reply.m_type = NetMessageType.LibraryDisconnect;
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