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

		// called by constructor
		private void SetupInternal()
		{
			m_releasedIncomingMessages = new Queue<NetIncomingMessage>();
			m_nextSequenceNumber = 1;
		}

		//
		// Network loop
		//
		private void Run()
		{
			LogDebug("Network thread started");

			while (!m_initiateShutdown)
			{
				try
				{
					Heartbeat();
				}
				catch (Exception ex)
				{
					LogWarning(ex.ToString());
				}

				// wait here to give cpu to other threads/processes
				Thread.Sleep(m_runSleepInMilliseconds);
			}

			//
			// perform shutdown
			//
			LogDebug("Shutting down...");

			lock (m_initializeLock)
			{
				// TODO: Send all outgoing message immediately

				try
				{
					if (m_socket != null)
					{
						m_socket.Shutdown(SocketShutdown.Receive);
						m_socket.Close(2);
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

		private void ReleaseMessage(NetIncomingMessage msg)
		{
			lock (m_releasedIncomingMessages)
				m_releasedIncomingMessages.Enqueue(msg);
		}

		private void Heartbeat()
		{
			// do connection heartbeats
			foreach (NetConnection conn in m_connections)
				conn.Heartbeat();

			// read from socket
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
					LogWarning(sx.ToString());
					return;
				}

				if (bytesReceived < 1)
					return;

				double now = NetTime.Now;

				IPEndPoint ipsender = (IPEndPoint)m_senderRemote;

				NetConnection sender = null;
				m_connectionLookup.TryGetValue(ipsender, out sender);

				if (sender != null)
					sender.m_lastHeardFrom = now;

				//
				// parse packet into messages
				//
				{
					bool isReliable = ((m_receiveBuffer[0] & 1) == 1);
					ushort sequenceNumber = (ushort)((m_receiveBuffer[0] >> 1) & (m_receiveBuffer[1] << 7));

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
							byte nr = m_receiveBuffer[ptr++];
							if (sender == null)
							{
								LogWarning("Received ping from non-connected peer");
								continue;
							}
							sender.HandlePing(nr);
							continue;
						}

						if (mtp == NetMessageType.LibraryPong)
						{
							byte nr = m_receiveBuffer[ptr++];
							if (sender == null)
							{
								LogWarning("Received pong from non-connected peer");
								continue;
							}
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
						Debug.Assert(payload.Length >= payloadLength, "Too small storage retrieved!");
						Buffer.BlockCopy(m_receiveBuffer, ptr, payload, 0, payloadLength);
						ptr += payloadLength;

						//
						// handle incoming message
						//
						switch (mtp)
						{
							case NetMessageType.Error:
								LogError("Malformed message; no message type!");
								break;
							default:
								throw new NotImplementedException();
						}
					}
				}
			}
			// heartbeat done
		}
	}
}