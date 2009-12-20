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
		private ushort m_nextSequenceNumber;
		internal byte[] m_macAddressBytes;

		private Queue<NetOutgoingMessage> m_unsentUnconnectedMessage;
		private Queue<IPEndPoint> m_unsentUnconnectedRecipients;

		/// <summary>
		/// Gets or sets the amount of time in milliseconds the network thread should sleep; recommended values 1 or 0
		/// </summary>
		public int NetworkThreadSleepTime
		{
			get { return m_runSleepInMilliseconds; }
			set { m_runSleepInMilliseconds = value; }
		}

		// called by constructor
		private void InitializeInternal()
		{
			m_releasedIncomingMessages = new Queue<NetIncomingMessage>();
			m_nextSequenceNumber = 1;
			m_unsentUnconnectedMessage = new Queue<NetOutgoingMessage>();
			m_unsentUnconnectedRecipients = new Queue<IPEndPoint>();

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
		}

		//
		// Network loop
		//
		private void Run()
		{
			LogDebug("Network thread started");

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
			
			// one final heartbeat, to get the disconnects out the door
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

			return;
		}

		internal ushort GetSequenceNumber()
		{
			ushort retval = m_nextSequenceNumber;
			m_nextSequenceNumber++;
			return retval;
		}

		internal void ReleaseMessage(NetIncomingMessage msg)
		{
			lock (m_releasedIncomingMessages)
				m_releasedIncomingMessages.Enqueue(msg);
		}

		private void Heartbeat()
		{
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
					ushort sequenceNumber = (ushort)(m_receiveBuffer[0] | (m_receiveBuffer[1] << 8));

					LogVerbose("Received packet " + sequenceNumber + " (" + bytesReceived + " bytes)");

					if (sender != null)
					{
						// TODO: if reliable; queue ack message, also update connection.m_lastSendRespondedTo
						HandleAcknowledge(sequenceNumber);
					}

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

						// ping/pong has known length
						if (mtp == NetMessageType.LibraryPing)
						{
							if (sender == null)
							{
								LogWarning("Received ping from non-connected peer");
								continue;
							}
							ushort nr = (ushort)(m_receiveBuffer[ptr] | (m_receiveBuffer[ptr + 1] << 8));
							ptr += 2;
							sender.HandlePing(nr);
							continue;
						}

						if (mtp == NetMessageType.LibraryPong)
						{
							if (sender == null)
							{
								LogWarning("Received pong from non-connected peer");
								continue;
							}
							ushort nr = (ushort)(m_receiveBuffer[ptr] | (m_receiveBuffer[ptr + 1] << 8));
							ptr += 2;
							sender.HandlePong(now, nr);
							continue;
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
							sender.HandleIncomingData(mtp, payload, payloadLength);
						}
					}
				}
			}
			// heartbeat done
		}

		private void HandleAcknowledge(ushort sequenceNumber)
		{
			// TODO: update received bitmask and send ack
		}

		private void HandleUnconnectedMessage(NetMessageType mtp, byte[] payload, int payloadLength, IPEndPoint senderEndPoint)
		{
			Debug.Assert(Thread.CurrentThread == m_networkThread);

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
					m_connections.Add(conn);
					m_connectionLookup[senderEndPoint] = conn;
					conn.m_connectionInitiator = false;
					conn.SetStatus(NetConnectionStatus.Connecting);

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
				case NetMessageType.LibraryDiscovery:
				case NetMessageType.LibraryDiscoveryResponse:
				case NetMessageType.LibraryNatIntroduction:
				case NetMessageType.LibraryPing:
				case NetMessageType.LibraryPong:
					throw new NotImplementedException();
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