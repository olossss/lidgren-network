using System;
using System.Collections.Generic;

namespace Lidgren.Network2
{
	public class NetClient : NetPeer
	{
		private NetConnection m_serverConnection;

		public NetConnection ServerConnection { get { return m_serverConnection; } }

		public NetClient(NetPeerConfiguration config)
			: base(config)
		{
		}

		/// <summary>
		/// Sends message to server
		/// </summary>
		public void SendMessage(NetOutgoingMessage msg, NetMessagePriority priority)
		{
			m_serverConnection.SendMessage(msg, priority);
		}
	}
}
