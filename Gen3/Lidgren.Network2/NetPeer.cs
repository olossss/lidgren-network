using System;
using System.Collections.Generic;
using System.Threading;
using System.Net;
using System.Net.Sockets;

namespace Lidgren.Network2
{
	//
	// This partial file holds public netpeer methods accessible to the application
	//
	public partial class NetPeer
	{
		private bool m_isInitialized;
		private bool m_initiateShutdown;
		private object m_initializeLock = new object();
		
		internal NetPeerConfiguration m_configuration;
		private NetPeerStatistics m_statistics;
		private Thread m_networkThread;

		protected List<NetConnection> m_connections;
		private Dictionary<IPEndPoint, NetConnection> m_connectionLookup;
		
		/// <summary>
		/// Gets a copy of the list of connections
		/// </summary>
		public List<NetConnection> Connections
		{
			get
			{
				lock (m_connections)
				{
					return new List<NetConnection>(m_connections);
				}
			}
		}

		public NetPeer(NetPeerConfiguration configuration)
		{
			m_configuration = configuration;
			m_connections = new List<NetConnection>();
			m_connectionLookup = new Dictionary<IPEndPoint, NetConnection>();
			m_senderRemote = (EndPoint)new IPEndPoint(IPAddress.Any, 0);
			m_statistics = new NetPeerStatistics();

			InitializeRecycling();
			InitializeInternal();
		}

		/// <summary>
		/// Binds to socket
		/// </summary>
		public void Initialize()
		{
			lock (m_initializeLock)
			{
				if (m_isInitialized)
					return;
				m_configuration.Lock();

				m_statistics.Reset();

				// bind to socket
				IPEndPoint iep = null;
				try
				{
					iep = new IPEndPoint(m_configuration.LocalAddress, m_configuration.Port);
					EndPoint ep = (EndPoint)iep;

					m_socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
					m_socket.ReceiveBufferSize = m_configuration.ReceiveBufferSize;
					m_socket.SendBufferSize = m_configuration.SendBufferSize;
					m_socket.Blocking = false;
					m_socket.Bind(ep);

					IPEndPoint boundEp = m_socket.LocalEndPoint as IPEndPoint;
					LogDebug("Socket bound to " + boundEp + ": " + m_socket.IsBound);

					m_receiveBuffer = new byte[m_configuration.ReceiveBufferSize];
					m_sendBuffer = new byte[m_configuration.SendBufferSize];

					// start network thread
					m_networkThread = new Thread(new ThreadStart(Run));
					m_networkThread.Name = "Lidgren network thread";
					m_networkThread.IsBackground = true;
					m_networkThread.Start();

					LogVerbose("Initialization done");

					// only set initialized if everything succeeds
					m_isInitialized = true;
				}
				catch (SocketException sex)
				{
					if (sex.SocketErrorCode == SocketError.AddressAlreadyInUse)
						throw new NetException("Failed to bind to port " + (iep == null ? "Null" : iep.ToString()) + " - Address already in use!", sex);
					throw;
				}
				catch (Exception ex)
				{
					throw new NetException("Failed to bind to " + (iep == null ? "Null" : iep.ToString()), ex);
				}
			}
		}

		internal void SendPacket(int numBytes, IPEndPoint target)
		{
			try
			{
#if DEBUG
				ushort packetNumber = (ushort)(m_sendBuffer[0] | (m_sendBuffer[1] << 8));
				LogVerbose("Sending packet P#" + packetNumber + " (" + numBytes + " bytes)");
#endif

				// TODO: Use SendToAsync()?
				int bytesSent = m_socket.SendTo(m_sendBuffer, 0, numBytes, SocketFlags.None, target);
				if (numBytes != bytesSent)
					LogWarning("Failed to send the full " + numBytes + "; only " + bytesSent + " bytes sent in packet!");

				m_statistics.m_sentPackets++;
			}
			catch (Exception ex)
			{
				LogError("Failed to send packet: " + ex);
			}
		}

		public void SendMessage(NetOutgoingMessage msg, NetConnection recipient, NetMessageChannel channel, NetMessagePriority priority)
		{
			if (msg.IsSent)
				throw new NetException("Message has already been sent!");
			msg.m_type = (NetMessageType)channel;
			recipient.EnqueueOutgoingMessage(msg, priority);
		}

		public void SendMessage(NetOutgoingMessage msg, IEnumerable<NetConnection> recipients, NetMessageChannel channel, NetMessagePriority priority)
		{
			if (msg.IsSent)
				throw new NetException("Message has already been sent!");
			msg.m_type = (NetMessageType)channel;
			foreach (NetConnection conn in recipients)
				conn.EnqueueOutgoingMessage(msg, priority);
		}

		public void SendUnconnectedMessage(NetOutgoingMessage msg, IPEndPoint recipient)
		{
			if (msg.IsSent)
				throw new NetException("Message has already been sent!");
			EnqueueUnconnectedMessage(msg, recipient);
		}

		public void SendUnconnectedMessage(NetOutgoingMessage msg, IEnumerable<IPEndPoint> recipients)
		{
			if (msg.IsSent)
				throw new NetException("Message has already been sent!");
			foreach (IPEndPoint ipe in recipients)
				EnqueueUnconnectedMessage(msg, ipe);
		}

		/// <summary>
		/// Read a pending message from any connection, if any
		/// </summary>
		public NetIncomingMessage ReadMessage()
		{
			if (m_releasedIncomingMessages.Count < 1)
				return null;

			lock (m_releasedIncomingMessages)
			{
				if (m_releasedIncomingMessages.Count < 1)
					return null;
				return m_releasedIncomingMessages.Dequeue();
			}
		}

		/// <summary>
		/// Create a connection to a remote endpoint
		/// </summary>
		public NetConnection Connect(string host, int port)
		{
			return Connect(new IPEndPoint(NetUtility.Resolve(host), port));
		}

		/// <summary>
		/// Create a connection to a remote endpoint
		/// </summary>
		public virtual NetConnection Connect(IPEndPoint remoteEndPoint)
		{
			if (!m_isInitialized)
				Initialize();

			if (m_connectionLookup.ContainsKey(remoteEndPoint))
				throw new NetException("Already connected to that endpoint!");

			NetConnection conn = new NetConnection(this, remoteEndPoint);

			// handle on network thread
			conn.m_connectRequested = true;
			conn.m_connectionInitiator = true;
			conn.SetStatus(NetConnectionStatus.Connecting, "Connecting");

			lock (m_connections)
			{
				m_connections.Add(conn);
				m_connectionLookup[remoteEndPoint] = conn;
			}

			return conn;
		}

		public void Shutdown(string bye)
		{
			if (m_socket == null)
				return; // already shut down

			LogDebug("Shutdown requested");

			// disconnect all connections
			lock (m_connections)
			{
				foreach (NetConnection conn in m_connections)
					conn.Disconnect(bye);
			}

			m_initiateShutdown = true;
		}
	}
}
