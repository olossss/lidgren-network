using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Net;

namespace Lidgren.Network2
{
	public class NetPeer
	{
		private bool m_isInitialized;
		private bool m_initiateShutdown;
		private int m_runSleepInMilliseconds = 1;
		private object m_initializeLock = new object();
		private byte[] m_receiveBuffer;
		private EndPoint m_senderRemote;

		private NetPeerConfiguration m_configuration;
		private Socket m_socket;
		private Thread m_networkThread;

		private List<NetConnection> m_connections;
		private Dictionary<IPEndPoint, NetConnection> m_connectionLookup;

		public NetPeer(NetPeerConfiguration configuration)
		{
			m_configuration = configuration;
			m_connections = new List<NetConnection>();
			m_connectionLookup = new Dictionary<IPEndPoint, NetConnection>();

			m_senderRemote = (EndPoint)new IPEndPoint(IPAddress.Any, 0);
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

				// bind to socket
				IPEndPoint iep = null;
				try
				{
					iep = new IPEndPoint(m_configuration.LocalAddress, m_configuration.Port);
					EndPoint ep = (EndPoint)iep;

					m_socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
					m_socket.Blocking = false;
					m_socket.Bind(ep);

					m_receiveBuffer = new byte[m_configuration.ReceiveBufferSize];

					// start network thread
					m_networkThread = new Thread(new ThreadStart(Run));
					m_networkThread.Name = "Lidgren network thread";
					m_networkThread.IsBackground = true;
					m_networkThread.Start();
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

		//
		// Network loop
		//
		private void Run()
		{
			while (!m_initiateShutdown)
			{
				try
				{
					Heartbeat();
				}
				catch (Exception ex)
				{
					// LogWrite("Heartbeat() failed on network thread: " + ex.Message);
				}

				// wait here to give cpu to other threads/processes
				Thread.Sleep(m_runSleepInMilliseconds);
			}

			//
			// perform shutdown
			//
			lock (m_initializeLock)
			{
				try
				{
					if (m_socket != null)
					{
						m_socket.Shutdown(SocketShutdown.Receive);
						m_socket.Close(2);
					}
				}
				finally
				{
					m_socket = null;
					m_isInitialized = false;
					m_initiateShutdown = false;
				}
			}

			return;
		}

		private void Heartbeat()
		{
			// do connection heartbeats
			foreach (NetConnection conn in m_connections)
				conn.Heartbeat();

			// read from socket
			while (true)
			{
				if (m_socket == null || m_socket.Available < 1)
					return;

				int bytesReceived = 0;
				try
				{
					bytesReceived = m_socket.ReceiveFrom(m_receiveBuffer, 0, m_receiveBuffer.Length, SocketFlags.None, ref m_senderRemote);
				}
				catch (SocketException)
				{
					// no good response to this yet
					return;
				}

				if (bytesReceived < 1)
					return;

				IPEndPoint ipsender = (IPEndPoint)m_senderRemote;

				NetConnection sender = null;
				m_connectionLookup.TryGetValue(ipsender, out sender);
				
				// TODO: create incoming message from packet
			}
		}

		public void Shutdown()
		{
			m_initiateShutdown = true;
		}
	}
}
