using System;
using System.Collections.Generic;
using System.Net;

namespace Lidgren.Network
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
		/// Disconnect from server
		/// </summary>
		/// <param name="byeMessage">reason for disconnect</param>
		public void Disconnect(string byeMessage)
		{
			NetConnection serverConnection = null;
			if (m_connections.Count > 0)
			{
				try
				{
					serverConnection = m_connections[0];
				}
				catch
				{
					// preempted!
				}
			}

			if (serverConnection == null)
			{
				LogWarning("Disconnect requested when not connected!");
				return;
			}

			serverConnection.Disconnect(byeMessage);
		}

		/// <summary>
		/// Sends message to server
		/// </summary>
		public void SendMessage(NetOutgoingMessage msg, NetDeliveryMethod channel, NetMessagePriority priority)
		{
			NetConnection serverConnection = ServerConnection;
			if (serverConnection == null)
			{
				LogError("Cannot send message, no server connection!");
				return;
			}
			serverConnection.SendMessage(msg, channel, priority);
		}
	}
}
