using System;
using System.Collections.Generic;

namespace Lidgren.Network
{
	public sealed partial class NetOutgoingMessage
	{
		// reference count before message can be recycled
		internal int m_inQueueCount;
		internal NetMessageType m_type;
		internal double m_sentTime;
		internal NetMessagePriority m_priority;

		/// <summary>
		/// Gets or sets the priority of this message. Higher priority messages are sent before lower ones.
		/// </summary>
		public NetMessagePriority Priority { get { return m_priority; } set { m_priority = value; } }

		/// <summary>
		/// Returns true if this message has been passed to SendMessage() already
		/// </summary>
		public bool IsSent { get { return m_inQueueCount > 0; } }

		internal NetOutgoingMessage()
		{
			Reset();
		}

		internal void Reset()
		{
			m_type = NetMessageType.Error;
			m_inQueueCount = 0;
			m_priority = NetMessagePriority.Normal;
		}

		internal int Encode(byte[] buffer, int ptr)
		{
			// flags
			buffer[ptr++] = (byte)m_type;

			int msgPayloadLength = LengthBytes;
			
			System.Diagnostics.Debug.Assert(msgPayloadLength < 32768);
			if (msgPayloadLength < 127)
			{
				buffer[ptr++] = (byte)msgPayloadLength;
			}
			else
			{
				buffer[ptr++] = (byte)((msgPayloadLength & 127) | 128);
				buffer[ptr++] = (byte)(msgPayloadLength >> 7);
			}

			if (msgPayloadLength > 0)
			{
				Buffer.BlockCopy(m_data, 0, buffer, ptr, msgPayloadLength);
				ptr += msgPayloadLength;
			}

			return ptr;
		}
	}
}
