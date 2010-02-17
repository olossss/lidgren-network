using System;
using System.Collections.Generic;
using System.Threading;
using System.Net;
using System.Net.Sockets;

namespace Lidgren.Network
{
	//
	// This partial file holds public netpeer methods accessible to the application
	//
	public partial class NetPeer
	{
		private NetPeerStatus m_status;
		private object m_initializeLock = new object();
		private int m_uniqueIdentifier;

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

		/// <summary>
		/// Returns the number of active connections
		/// </summary>
		public int ConnectionsCount
		{
			get { return m_connections.Count; }
		}

		/// <summary>
		/// Statistics on this NetPeer since it was initialized
		/// </summary>
		public NetPeerStatistics Statistics
		{
			get { return m_statistics; }
		}

		/// <summary>
		/// Returns the configuration of the netpeer
		/// </summary>
		public NetPeerConfiguration Configuration { get { return m_configuration; } }

		public NetPeer(NetPeerConfiguration configuration)
		{
			m_status = NetPeerStatus.NotRunning;
			m_configuration = configuration;
			m_connections = new List<NetConnection>();
			m_connectionLookup = new Dictionary<IPEndPoint, NetConnection>();
			m_senderRemote = (EndPoint)new IPEndPoint(IPAddress.Any, 0);
			m_statistics = new NetPeerStatistics(this);

			// NetPeer.Recycling stuff
			m_storagePool = new List<byte[]>();
			m_incomingMessagesPool = new Queue<NetIncomingMessage>();

			// NetPeer.Internal stuff
			m_releasedIncomingMessages = new Queue<NetIncomingMessage>();
			m_unsentUnconnectedMessage = new Queue<NetOutgoingMessage>();
			m_unsentUnconnectedRecipients = new Queue<IPEndPoint>();
		}

		/// <summary>
		/// Binds to socket
		/// </summary>
		public void Start()
		{
			if (m_status != NetPeerStatus.NotRunning)
			{
				// already running! Just ignore...
				LogWarning("Start() called on already running NetPeer - ignoring.");
				return;
			}

			m_status = NetPeerStatus.Starting;

			InternalStart();

			m_configuration.VerifyAndLock();

			// start network thread
			m_networkThread = new Thread(new ThreadStart(Run));
			m_networkThread.Name = "Lidgren network thread";
			m_networkThread.IsBackground = true;
			m_networkThread.Start();

			// allow some time for network thread to start up in case they call Connect() immediately
			Thread.Sleep(3);
		}

		public void SendMessage(NetOutgoingMessage msg, NetConnection recipient, NetDeliveryMethod deliveryMethod)
		{
			SendMessage(msg, recipient, deliveryMethod, 0, NetMessagePriority.Normal);
		}

		public void SendMessage(NetOutgoingMessage msg, NetConnection recipient, NetDeliveryMethod deliveryMethod, int channel, NetMessagePriority priority)
		{
			if (msg.IsSent)
				throw new NetException("Message has already been sent!");
			if (channel < 0 || channel > 63)
				throw new NetException("Channel must be between 0 and 63");
			if (channel != 0 && (deliveryMethod == NetDeliveryMethod.Unreliable || deliveryMethod == NetDeliveryMethod.ReliableUnordered))
				throw new NetException("Channel must be 0 for Unreliable and ReliableUnordered");

			msg.m_type = (NetMessageType)((int)deliveryMethod + channel);
			
			recipient.EnqueueOutgoingMessage(msg, priority);
		}

		public void SendMessage(NetOutgoingMessage msg, IEnumerable<NetConnection> recipients, NetDeliveryMethod deliveryMethod, int channel, NetMessagePriority priority)
		{
			if (msg.IsSent)
				throw new NetException("Message has already been sent!");
			if (channel < 0 || channel > 63)
				throw new NetException("Channel must be between 0 and 63");
			if (channel != 0 && (deliveryMethod == NetDeliveryMethod.Unreliable || deliveryMethod == NetDeliveryMethod.ReliableUnordered))
				throw new NetException("Channel must be 0 for Unreliable and ReliableUnordered");

			msg.m_type = (NetMessageType)((int)deliveryMethod + channel);

			foreach (NetConnection conn in recipients)
				conn.EnqueueOutgoingMessage(msg, priority);
		}

		public void SendUnconnectedMessage(NetOutgoingMessage msg, IPEndPoint recipient)
		{
			if (msg.IsSent)
				throw new NetException("Message has already been sent!");
			msg.m_type = NetMessageType.UserUnreliable; // sortof not applicable
			EnqueueUnconnectedMessage(msg, recipient);
		}

		public void SendUnconnectedMessage(NetOutgoingMessage msg, IEnumerable<IPEndPoint> recipients)
		{
			if (msg.IsSent)
				throw new NetException("Message has already been sent!");
			msg.m_type = NetMessageType.UserUnreliable; // sortof not applicable
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
			return Connect(new IPEndPoint(NetUtility.Resolve(host), port), null);
		}

		/// <summary>
		/// Create a connection to a remote endpoint
		/// </summary>
		public NetConnection Connect(string host, int port, byte[] hailData)
		{
			return Connect(new IPEndPoint(NetUtility.Resolve(host), port), hailData);
		}

		/// <summary>
		/// Create a connection to a remote endpoint
		/// </summary>
		public virtual NetConnection Connect(IPEndPoint remoteEndPoint, byte[] hailData)
		{
			if (m_status == NetPeerStatus.NotRunning)
				throw new NetException("Must call Start() first");

			if (m_connectionLookup.ContainsKey(remoteEndPoint))
				throw new NetException("Already connected to that endpoint!");

			NetConnection conn = new NetConnection(this, remoteEndPoint);
			conn.m_localHailData = hailData;

			// handle on network thread
			conn.m_connectRequested = true;
			conn.m_connectionInitiator = true;

			lock (m_connections)
			{
				m_connections.Add(conn);
				m_connectionLookup[remoteEndPoint] = conn;
			}

			return conn;
		}

		[System.Diagnostics.Conditional("DEBUG")]
		internal void VerifyNetworkThread()
		{
			Thread ct = System.Threading.Thread.CurrentThread;
			if (ct != m_networkThread)
				throw new NetException("Executing on wrong thread! Should be library system thread (is " + ct.Name + " mId " + ct.ManagedThreadId + ")");
		}

		public void Shutdown(string bye)
		{
			// called on user thread

			if (m_socket == null)
				return; // already shut down

			LogDebug("Shutdown requested");
			m_status = NetPeerStatus.ShutdownRequested;

			// disconnect all connections
			lock (m_connections)
			{
				foreach (NetConnection conn in m_connections)
					conn.Disconnect(bye);
			}
		}
	}
}
