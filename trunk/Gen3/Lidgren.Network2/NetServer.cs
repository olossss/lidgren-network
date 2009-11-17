using System;
using System.Collections.Generic;

namespace Lidgren.Network2
{
	public class NetServer : NetPeer
	{
		public NetServer(NetPeerConfiguration config)
			: base(config)
		{
		}

		/// <summary>
		/// Sends message to all connected clients
		/// </summary>
		public void SendToAll(NetOutgoingMessage msg, NetMessagePriority priority)
		{
			if (msg.IsSent)
				throw new NetException("Message has already been sent!");
			foreach (NetConnection conn in m_connections)
				conn.EnqueueOutgoingMessage(msg, priority);
		}
	}
}
