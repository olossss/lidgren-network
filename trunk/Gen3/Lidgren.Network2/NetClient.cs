using System;

namespace Lidgren.Network2
{
	public class NetClient : NetPeer
	{
		public NetClient(NetPeerConfiguration config)
			: base(config)
		{
			// force this to false
			config.AcceptIncomingConnections = false;
		}
	}
}
