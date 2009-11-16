using System;
using System.Collections.Generic;
using System.Net;

namespace Lidgren.Network2
{
	public partial class NetConnection
	{
		private NetPeer m_owner;
		private IPEndPoint m_remoteEndPoint;
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
		}

		// run on network thread
		internal void Heartbeat()
		{
			// TODO: send ack messages

			// TODO: resend reliable messages

			// send unsent messages; high priority first
			byte[] buffer = m_owner.m_sendBuffer;
			int ptr = 0;
			bool isPacketReliable = false;
			int mtu = m_owner.m_configuration.MaximumTransmissionUnit;
			for (int i = 2; i >= 0; i--)
			{
				Queue<NetOutgoingMessage> queue = m_unsentMessages[i];
				if (queue.Count < 1)
					continue;

				NetOutgoingMessage msg;
				lock (queue)
					msg = queue.Dequeue();

				int msgPayloadLength = msg.LengthBytes;

				if (ptr + 3 + msgPayloadLength > mtu)
				{
					// send packet and start new packet
					m_owner.SendPacket(ptr, m_remoteEndPoint);
					ptr = 0;
				}

				if (ptr == 0)
				{
					// encode packet start
					ushort packetSequenceNumber = m_owner.GetSequenceNumber();
					buffer[ptr++] = (byte)((packetSequenceNumber & 127) << 1);
					buffer[ptr++] = (byte)((packetSequenceNumber << 7) & 255);
					isPacketReliable = false;
				}

				//
				// encode message
				//

				// set packet reliability flag
				if (!isPacketReliable && msg.m_type >= NetMessageType.UserReliableUnordered )
				{
					buffer[0] |= 1;
					isPacketReliable = true;
				}

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
				
				Buffer.BlockCopy(msg.m_data, 0, buffer, ptr, msgPayloadLength);
			}
		}

		public void SendMessage(NetOutgoingMessage msg, NetMessagePriority priority)
		{
			Queue<NetOutgoingMessage> queue = m_unsentMessages[(int)priority];
			lock (queue)
				queue.Enqueue(msg);
		}
		
	}
}
