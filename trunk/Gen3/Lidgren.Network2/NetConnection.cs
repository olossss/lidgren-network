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

		internal NetConnection(NetPeer owner, IPEndPoint remoteEndPoint)
		{
			m_owner = owner;
			m_remoteEndPoint = remoteEndPoint;
		}

		internal void Heartbeat()
		{
		}

		public void SendMessage(NetOutgoingMessage msg)
		{
		}

		
	}
}
