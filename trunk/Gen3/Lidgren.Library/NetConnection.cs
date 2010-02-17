using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;

namespace Lidgren.Network
{
	public partial class NetConnection
	{
		private NetPeer m_owner;
		internal IPEndPoint m_remoteEndPoint;
		internal double m_lastHeardFrom;
		private Queue<NetOutgoingMessage>[] m_unsentMessages; // low, normal, high

		internal PendingConnectionStatus m_pendingStatus = PendingConnectionStatus.NotPending;
		internal string m_pendingDenialReason;
		
		public object Tag;

		/// <summary>
		/// Gets the remote endpoint for this connection
		/// </summary>
		public IPEndPoint RemoteEndPoint { get { return m_remoteEndPoint; } }

		internal NetConnection(NetPeer owner, IPEndPoint remoteEndPoint)
		{
			m_owner = owner;
			m_remoteEndPoint = remoteEndPoint;
			m_unsentMessages = new Queue<NetOutgoingMessage>[3];
			m_unsentMessages[0] = new Queue<NetOutgoingMessage>(4);
			m_unsentMessages[1] = new Queue<NetOutgoingMessage>(8);
			m_unsentMessages[2] = new Queue<NetOutgoingMessage>(4);
			m_status = NetConnectionStatus.None;
			m_isPingInitialized = false;
			m_nextKeepAlive = NetTime.Now + 5.0;
			m_pingSendTime = NetTime.Now;

			m_lastSendRespondedTo = NetTime.Now;

			ResetSlidingWindow();
		}

		// run on network thread
		internal void Heartbeat()
		{
			m_owner.VerifyNetworkThread();

			double now = NetTime.Now;

			if (m_connectRequested)
				SendConnect();

			if (m_disconnectRequested)
			{
				// let high prio stuff slip past before disconnecting
				ExecuteDisconnect(NetMessagePriority.Normal, true);
			}

			// window closed?
			if (CanSend() == false && !m_disconnectRequested)
				return;

			// keepalive
			KeepAliveHeartbeat(now);

			// send unsent messages; high priority first
			byte[] buffer = m_owner.m_sendBuffer;
			int ptr = 0;
			WindowSlot packetWindowSlot = null;
			for (int i = 2; i >= 0; i--)
			{
				Queue<NetOutgoingMessage> queue = m_unsentMessages[i];

				while (queue.Count > 0)
				{
					NetOutgoingMessage msg;
					lock (queue)
						msg = queue.Peek();

					int msgPayloadLength = msg.LengthBytes;

					if (ptr > 0 && ptr + 3 + msgPayloadLength > m_owner.m_configuration.MaximumTransmissionUnit)
					{
						// send packet and start new packet
						m_owner.SendPacket(ptr, m_remoteEndPoint);
						ptr = 0;
					}

					if (ptr == 0)
					{
						if (!CanSend())
						{
							ptr = -1;
							break; // window full
						}
						packetWindowSlot = PrepareSend(now, out ptr);
					}

					// previously just peeked; now dequeue for real
					lock (queue)
						queue.Dequeue();

					msg.m_sentTime = now;

					//
					// encode message
					//

					ptr = msg.Encode(buffer, ptr);
	
					if (msg.m_type >= NetMessageType.UserReliableUnordered)
					{
						// message is reliable, store for resend
						packetWindowSlot.StoredReliableMessages.Add(msg);
					}
					else
					{
						Interlocked.Decrement(ref msg.m_inQueueCount);
					}
				}

				// Window full? no idea trying the other 
				if (ptr == -1)
					break;
			}

			if (ptr > 0)
				m_owner.SendPacket(ptr, m_remoteEndPoint);

			if (m_disconnectRequested)
				FinishDisconnect();
		}

		public void SendMessage(NetOutgoingMessage msg, NetDeliveryMethod channel, NetMessagePriority priority)
		{
			if (msg.IsSent)
				throw new NetException("Message has already been sent!");
			msg.m_type = (NetMessageType)channel;
			EnqueueOutgoingMessage(msg, priority);
		}

		internal void EnqueueOutgoingMessage(NetOutgoingMessage msg, NetMessagePriority priority)
		{
			Queue<NetOutgoingMessage> queue = m_unsentMessages[(int)priority];
			lock (queue)
				queue.Enqueue(msg);
			
			Interlocked.Increment(ref msg.m_inQueueCount);
		}

		public void Disconnect(string byeMessage)
		{
			// called on user thread
			if (m_status == NetConnectionStatus.None || m_status == NetConnectionStatus.Disconnected)
				return;
			m_owner.LogVerbose("Disconnect requested for " + this);
			m_disconnectByeMessage = byeMessage;
			m_disconnectRequested = true;
		}

		internal void HandleReceivedConnectedMessage(double now, NetMessageType mtp, byte[] payload, int payloadLength)
		{
			m_owner.VerifyNetworkThread();

			try
			{
				if (mtp < NetMessageType.LibraryNatIntroduction)
				{
					HandleIncomingLibraryData(now, mtp, payload, payloadLength);
					return;
				}

				if (m_owner.m_configuration.IsMessageTypeEnabled(NetIncomingMessageType.Data))
				{
					// TODO: propagate NetMessageType here to incoming message, exposing it to app?

					//
					// TODO: do reliabilility, sequence rejecting etc here
					//

					// it's an application data message
					NetIncomingMessage im = m_owner.CreateIncomingMessage(NetIncomingMessageType.Data, payload, payloadLength);
					im.m_senderConnection = this;
					im.m_senderEndPoint = m_remoteEndPoint;

					m_owner.LogVerbose("Releasing " + im);
					m_owner.ReleaseMessage(im);
					return;
				}
			}
			catch (Exception ex)
			{
#if DEBUG
				throw new NetException("Message generated exception: " + ex, ex);
#else
				m_owner.LogError("Message generated exception: " + ex);
				return;
#endif
			}

			throw new NetException("Unhandled type " + mtp);
		}

		private void HandleIncomingLibraryData(double now, NetMessageType mtp, byte[] payload, int payloadLength)
		{
			m_owner.VerifyNetworkThread();

			switch (mtp)
			{
				case NetMessageType.Error:
					m_owner.LogWarning("Received NetMessageType.Error message!");
					break;
				case NetMessageType.LibraryConnect:
				case NetMessageType.LibraryConnectResponse:
				case NetMessageType.LibraryConnectionEstablished:
				case NetMessageType.LibraryDisconnect:
					HandleIncomingHandshake(mtp, payload, payloadLength);
					break;
				case NetMessageType.LibraryKeepAlive:
					// no operation, we just want the acks
					break;
				case NetMessageType.LibraryPing:
					if (payloadLength > 0)
						HandleIncomingPing(payload[0]);
					else
						m_owner.LogWarning("Received malformed ping");
					break;
				case NetMessageType.LibraryPong:
					if (payloadLength > 0)
						HandleIncomingPong(now, payload[0]);
					else
						m_owner.LogWarning("Received malformed pong");
					break;
				default:
					throw new NotImplementedException();
			}
		}

		public void Approve()
		{
			if (m_pendingStatus != PendingConnectionStatus.Pending)
			{
				m_owner.LogWarning("Approve() called on non-pending connection!");
				return;
			}
			m_pendingStatus = PendingConnectionStatus.Approved;
		}

		public void Deny(string reason)
		{
			if (m_pendingStatus != PendingConnectionStatus.Pending)
			{
				m_owner.LogWarning("Deny() called on non-pending connection!");
				return;
			}
			m_pendingStatus = PendingConnectionStatus.Denied;
			m_pendingDenialReason = reason;
		}

		internal void Dispose()
		{
			m_owner = null;
			m_unsentMessages = null;
		}

		public override string ToString()
		{
			return "[NetConnection to " + m_remoteEndPoint + "]";
		}
	}
}
