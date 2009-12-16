using System;
using System.Collections.Generic;

namespace Lidgren.Network2
{
	public class NetServer : NetPeer
	{
		public NetServer(NetPeerConfiguration config)
			: base(config)
		{
			// force this to true
			config.AcceptIncomingConnections = true;
		}
	}
}
