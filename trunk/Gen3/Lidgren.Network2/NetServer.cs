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
		public void SendMessage(NetOutgoingMessage msg)
		{
			// TODO: implement
			throw new NotImplementedException();
		}
	}
}
