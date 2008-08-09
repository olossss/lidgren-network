using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace Lidgren.Network
{
	/// <summary>
	/// A client which can initiate and accept multiple connections
	/// </summary>
	public class NetPeer : NetServer
	{
		public NetPeer(NetConfiguration config)
			: base(config)
		{
			m_allowOutgoingConnections = true;
		}

		/// <summary>
		/// Connects to the specified host on the specified port; passing hailData to the server
		/// </summary>
		public void Connect(string host, int port, byte[] hailData)
		{
			IPAddress ip = NetUtility.Resolve(host);
			if (ip == null)
				throw new NetException("Unable to resolve host");
			Connect(new IPEndPoint(ip, port), hailData);
		}

		/// <summary>
		/// Connects to the specified endpoint; passing hailData to the server
		/// </summary>
		private void Connect(IPEndPoint remoteEndpoint, byte[] hailData)
		{
			// ensure we're bound to socket
			if (!m_isBound)
				Start();

			// find empty slot
			if (m_connections.Count >= m_config.MaxConnections)
				throw new NetException("No available slots!");

			// will intiate handshake
			NetConnection connection = new NetConnection(this, remoteEndpoint, hailData);
			lock(m_connections)
				m_connections.Add(connection);
			m_connectionLookup.Add(remoteEndpoint, connection);			
			connection.Connect();
		}

		/// <summary>
		/// Emit a discovery signal to your subnet
		/// </summary>
		public void DiscoverLocalPeers(int port)
		{
			if (!m_isBound)
				Start();

			NetBuffer data = CreateBuffer();
			data.InternalEnsureBufferSize(m_config.ApplicationIdentifier.Length + 2);
			data.Write((byte)NetSystemType.Discovery);
			data.Write(m_config.ApplicationIdentifier);

			LogWrite("Broadcasting local peer discovery ping...");
			BroadcastUnconnectedMessage(data, port);
		}

	}
}
