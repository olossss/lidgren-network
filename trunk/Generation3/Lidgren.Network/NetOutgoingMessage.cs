using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Lidgren.Network
{
	[DebuggerDisplay("LengthBytes={LengthBytes}")]
	public sealed partial class NetOutgoingMessage
	{
		// reference count before message can be recycled
		internal int m_inQueueCount;
		internal NetMessageType m_type;
		internal NetMessageLibraryType m_libType;
		internal ushort m_sequenceNumber;

		internal double m_lastSentTime;
		internal double m_nextResendTime;
		internal int m_numResends;

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
			m_bitLength = 0;
			m_type = NetMessageType.Error;
			m_inQueueCount = 0;
			m_numResends = 0;
		}

		internal static int EncodeAcksMessage(byte[] buffer, int ptr, NetConnection conn, int maxBytesPayload)
		{
			buffer[ptr++] = (byte)NetMessageType.Library;
			buffer[ptr++] = (byte)NetMessageLibraryType.Acknowledge;

			Queue<int> acks = conn.m_acknowledgesToSend;

			int maxAcks = maxBytesPayload / 3;
			int acksToEncode = (acks.Count < maxAcks ? acks.Count : maxAcks);

			int msgPayloadLength = acksToEncode * 3;
			if (msgPayloadLength < 127)
			{
				buffer[ptr++] = (byte)msgPayloadLength;
			}
			else
			{
				buffer[ptr++] = (byte)((msgPayloadLength & 127) | 128);
				buffer[ptr++] = (byte)(msgPayloadLength >> 7);
			}

			for (int i = 0; i < acksToEncode; i++)
			{
				int ack = acks.Dequeue();
				buffer[ptr++] = (byte)ack; // message type
				buffer[ptr++] = (byte)(ack >> 8); // seqnr low
				buffer[ptr++] = (byte)(ack >> 16); // seqnr high
			}

			return ptr;
		}

		internal int Encode(byte[] buffer, int ptr, NetConnection conn)
		{
			// message type
			buffer[ptr++] = (byte)m_type;

			if (m_type == NetMessageType.Library)
				buffer[ptr++] =(byte)m_libType;

			// channel sequence number
			if (m_type >= NetMessageType.UserSequenced)
			{
				if (conn == null)
					throw new NetException("Trying to encode NetMessageType " + m_type + " to unconnected endpoint!");
				m_sequenceNumber = conn.GetSendSequenceNumber(m_type);
				buffer[ptr++] = (byte)m_sequenceNumber;
				buffer[ptr++] = (byte)(m_sequenceNumber >> 8);
			}

			// payload length
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

			// payload
			if (msgPayloadLength > 0)
			{
				Buffer.BlockCopy(m_data, 0, buffer, ptr, msgPayloadLength);
				ptr += msgPayloadLength;
			}

			return ptr;
		}
	}
}
