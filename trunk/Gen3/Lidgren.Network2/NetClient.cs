using System;
using System.Collections.Generic;
using System.Net;

namespace Lidgren.Network2
{
	public class NetClient : NetPeer
	{
		/// <summary>
		/// Gets the connection to the server, if any
		/// </summary>
		public NetConnection ServerConnection
		{
			get
			{
				return (m_connections.Count > 0 ? m_connections[0] : null);
			}
		}

		public NetClient(NetPeerConfiguration config)
			: base(config)
		{
			config.AcceptIncomingConnections = false;
		}

		/// <summary>
		/// Connect to a server
		/// </summary>
		public override NetConnection Connect(IPEndPoint remoteEndPoint)
		{
			return base.Connect(remoteEndPoint);
		}

		/// <summary>
		/// Sends message to server
		/// </summary>
		public void SendMessage(NetOutgoingMessage msg, NetMessagePriority priority)
		{
			NetConnection serverConnection = ServerConnection;
			if (serverConnection == null)
			{
				LogError("Cannot send message, no server connection!");
				return;
			}
			serverConnection.SendMessage(msg, priority);
		}
	}
}
