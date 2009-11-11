using System;
using System.Collections.Generic;
using System.Net;

namespace Lidgren.Network2
{
	public sealed class NetIncomingMessage
	{
		private NetIncomingMessageType m_messageType;
		private IPEndPoint m_senderEndPoint;
		private NetConnection m_senderConnection;

		public NetIncomingMessageType MessageType { get { return m_messageType; } }
		public IPEndPoint SenderEndpoint { get { return m_senderEndPoint; } }
		public NetConnection SenderConnection { get { return m_senderConnection; } }
	
		/// <summary>
		/// Recycle this message to the library for reuse
		/// </summary>
		public void Recycle()
		{
			throw new NotImplementedException();
		}
	}
}
