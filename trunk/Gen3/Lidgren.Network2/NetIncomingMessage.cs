using System;
using System.Collections.Generic;
using System.Net;

namespace Lidgren.Network2
{
	public sealed partial class NetIncomingMessage
	{
		internal byte[] m_data;
		private int m_bitLength;

		private NetIncomingMessageType m_messageType;
		private IPEndPoint m_senderEndPoint;
		private NetConnection m_senderConnection;

		public NetIncomingMessageType MessageType { get { return m_messageType; } internal set { m_messageType = value; } }
		public IPEndPoint SenderEndpoint { get { return m_senderEndPoint; } internal set { m_senderEndPoint = value; } }
		public NetConnection SenderConnection { get { return m_senderConnection; } internal set { m_senderConnection = value; } }

		internal NetIncomingMessage()
		{
		}

		internal void Reset()
		{
			m_bitLength = 0;
			m_readPosition = 0;
		}
	}
}
