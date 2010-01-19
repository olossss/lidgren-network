using System;
using System.Collections.Generic;
using System.Net;

namespace Lidgren.Network2
{
	public sealed partial class NetIncomingMessage
	{
		internal byte[] m_data;
		internal int m_bitLength;

		internal NetIncomingMessageType m_messageType;
		internal IPEndPoint m_senderEndPoint;
		internal NetConnection m_senderConnection;

		/// <summary>
		/// Type of data contained in this message
		/// </summary>
		public NetIncomingMessageType MessageType { get { return m_messageType; } }

		/// <summary>
		/// IPEndPoint of sender, if any
		/// </summary>
		public IPEndPoint SenderEndPoint { get { return m_senderEndPoint; } }

		/// <summary>
		/// NetConnection of sender, if any
		/// </summary>
		public NetConnection SenderConnection { get { return m_senderConnection; } }

		internal NetIncomingMessage()
		{
		}

		internal void Reset()
		{
			m_bitLength = 0;
			m_readPosition = 0;
		}

		public override string ToString()
		{
			return "[NetIncomingMessage " + m_messageType + ", " + m_bitLength + " bits]";
		}
	}
}
