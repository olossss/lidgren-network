using System;
using System.Collections.Generic;
using System.Net;

namespace Lidgren.Network2
{
	public class NetConnection
	{
		private IPEndPoint m_remoteEndPoint;

		internal NetConnection(IPEndPoint remoteEndPoint)
		{
			m_remoteEndPoint = remoteEndPoint;
		}

		internal void Heartbeat()
		{
		}
	}
}
