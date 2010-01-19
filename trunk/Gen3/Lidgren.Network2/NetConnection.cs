using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;

namespace Lidgren.Network2
{
	public partial class NetConnection
	{
		private NetPeer m_owner;
		internal IPEndPoint m_remoteEndPoint;
		internal double m_lastHeardFrom;
		private Queue<NetOutgoingMessage>[] m_unsentMessages; // low, normal, high

		internal NetConnection(NetPeer owner, IPEndPoint remoteEndPoint)
		{
			m_owner = owner;
			m_remoteEndPoint = remoteEndPoint;
			m_unsentMessages = new Queue<NetOutgoingMessage>[3];
			m_unsentMessages[0] = new Queue<NetOutgoingMessage>(4);
			m_unsentMessages[1] = new Queue<NetOutgoingMessage>(8);
			m_unsentMessages[2] = new Queue<NetOutgoingMessage>(4);
			m_status = NetConnectionStatus.Disconnected;
			m_isPingInitialized = false;
			m_nextKeepAlive = double.MaxValue;

			m_latencyWindowSize = owner.m_configuration.LatencyCalculationWindowSize;

			ReliabilityCtor();
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
				ExecuteDisconnect(NetMessagePriority.Normal);
			}

			// keepalive
			KeepAliveHeartbeat(now);

			// TODO: resend nack:ed reliable messages

			// send unsent messages; high priority first
			byte[] buffer = m_owner.m_sendBuffer;
			int ptr = 0;
			int mtu = m_owner.m_configuration.MaximumTransmissionUnit;
			int packetSlot = 0;
			for (int i = 2; i >= 0; i--)
			{
				Queue<NetOutgoingMessage> queue = m_unsentMessages[i];

				while (queue.Count > 0)
				{
					NetOutgoingMessage msg;
					lock (queue)
						msg = queue.Peek();

					int msgPayloadLength = msg.LengthBytes;

					if (ptr + 3 + msgPayloadLength > mtu)
					{
						// send packet and start new packet
						m_owner.SendPacket(ptr, m_remoteEndPoint);
						ptr = 0;
					}

					if (ptr == 0)
					{
						int packetSequenceNumber;
						if (!GetSendPacket(now, out packetSequenceNumber, out packetSlot))
							break; // window full

						// encode packet start
						buffer[0] = (byte)(packetSequenceNumber & 255);
						buffer[1] = (byte)((packetSequenceNumber >> 8) & 255);
						ptr = 2;
					}

					// previously just peeked; now dequeue for real
					queue.Dequeue();

					msg.m_sentTime = now;

					//
					// encode message
					//

					// flags
					if (msgPayloadLength < 256)
					{
						buffer[ptr++] = (byte)((int)msg.m_type << 2);
						buffer[ptr++] = (byte)msgPayloadLength;
					}
					else
					{
						buffer[ptr++] = (byte)(((int)msg.m_type << 2) | 1);
						buffer[ptr++] = (byte)(msgPayloadLength & 255);
						buffer[ptr++] = (byte)((msgPayloadLength << 8) & 255);
					}

					if (msgPayloadLength > 0)
						Buffer.BlockCopy(msg.m_data, 0, buffer, ptr, msgPayloadLength);
	
					if (msg.m_type >= NetMessageType.UserReliableUnordered)
					{
						// message is reliable
						m_packetList[packetSlot].Add(msg);
					}
					else
					{
						Interlocked.Decrement(ref msg.m_inQueueCount);
					}

					// piggyback acks?
					if (m_acksToSend.Count > 0)
					{
						int ackNr;
						lock (m_acksToSend)
						{
							while (m_acksToSend.Count > 0 && ptr + 4 <= mtu)
							{
								ackNr = m_acksToSend.Dequeue();

								m_owner.LogVerbose("Sending, by piggyback, ack R#" + ackNr);

								// hardcoded message
								buffer[ptr++] = (byte)((int)NetMessageType.LibraryAcknowledge << 2);
								buffer[ptr++] = 2; // two bytes length
								buffer[ptr++] = (byte)(ackNr & 255);
								buffer[ptr++] = (byte)((ackNr >> 8) & 255);
							}
						}
					}
				}

				// GetSendPacket() will set packetSlot to -1 when window is full
				if (packetSlot == -1)
					break;
			}

			if (ptr > 0)
				m_owner.SendPacket(ptr, m_remoteEndPoint);
			ptr = 0;

			// any acks left that wasn't piggybacked?
			// TODO: add small delay here to enable better piggybacking
			if (m_acksToSend.Count > 0)
			{
				int ackNr;
				buffer[ptr++] = 0; // zero packet number = no ack
				buffer[ptr++] = 0;
				lock (m_acksToSend)
				{
					while (m_acksToSend.Count > 0 && ptr + 4 <= mtu)
					{
						ackNr = m_acksToSend.Dequeue();

						m_owner.LogVerbose("Explicitly sending ack for R#" + ackNr);

						buffer[ptr++] = (byte)((int)NetMessageType.LibraryAcknowledge << 2);
						buffer[ptr++] = 2; // two bytes length
						buffer[ptr++] = (byte)(ackNr & 255);
						buffer[ptr++] = (byte)((ackNr >> 8) & 255);
					}
				}
			}

			if (ptr > 0)
				m_owner.SendPacket(ptr, m_remoteEndPoint);
		}

		public void SendMessage(NetOutgoingMessage msg, NetMessageChannel channel, NetMessagePriority priority)
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

			if (m_status == NetConnectionStatus.Disconnected)
				return;
			m_owner.LogVerbose("Disconnect requested for " + this);
			m_disconnectByeMessage = byeMessage;
			m_disconnectRequested = true;
		}

		internal void HandleReceivedConnectedMessage(double now, NetMessageType mtp, byte[] payload, int payloadLength)
		{
			m_owner.VerifyNetworkThread();

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
				case NetMessageType.LibraryAcknowledge:
					int nr = payload[0] | (payload[1] << 8);
					ReceiveAcknowledge(now, nr);
					break;
				case NetMessageType.LibraryKeepAlive:
					// no op, we just want the acks, maam
					m_owner.LogVerbose("Received keepalive (no action)");
					break;
				default:
					throw new NotImplementedException();
			}
		}

		public override string ToString()
		{
			return "[NetConnection to " + m_remoteEndPoint + "]";
		}
	}
}
