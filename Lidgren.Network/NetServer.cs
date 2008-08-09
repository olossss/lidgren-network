/* Copyright (c) 2008 Michael Lidgren

Permission is hereby granted, free of charge, to any person obtaining a copy of this software
and associated documentation files (the "Software"), to deal in the Software without
restriction, including without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom
the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or
substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE
USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Lidgren.Network
{
	/// <summary>
	/// A server which can accept connections from multiple NetClients
	/// </summary>
	public class NetServer : NetBase
	{
		protected List<NetConnection> m_connections;
		protected Dictionary<IPEndPoint, NetConnection> m_connectionLookup;
		protected bool m_allowOutgoingConnections; // used by NetPeer

		private NetQueue<NetMessage> m_lockedMessagePool;
		private NetQueue<NetBuffer> m_lockedBufferPool;

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

		public NetServer(NetConfiguration config)
			: base(config)
		{
			m_connections = new List<NetConnection>();
			m_connectionLookup = new Dictionary<IPEndPoint, NetConnection>();
			m_messagePool = new NetPool<NetMessage>(256, 4);
			m_bufferPool = new NetPool<NetBuffer>(256, 4);
			m_lockedMessagePool = new NetQueue<NetMessage>();
			m_lockedBufferPool = new NetQueue<NetBuffer>();
		}
		
		/// <summary>
		/// Reads and sends messages from the network
		/// </summary>
		protected override void Heartbeat()
		{
			double now = NetTime.Now;

			if (m_shutdownRequested)
			{
				PerformShutdown(m_shutdownReason);
				return;
			}

			// drain locked pools
			lock (m_lockedBufferPool)
			{
				NetBuffer lb;
				while ((lb = m_lockedBufferPool.Dequeue()) != null)
					m_bufferPool.Push(lb);
			}
			lock (m_lockedMessagePool)
			{
				NetMessage lm;
				while ((lm = m_lockedMessagePool.Dequeue()) != null)
					m_messagePool.Push(lm);
			}

			// read messages from network
			BaseHeartbeat(now);

			lock (m_connections)
			{
				List<NetConnection> deadConnections = null;
				foreach (NetConnection conn in m_connections)
				{
					if (conn.m_status == NetConnectionStatus.Disconnected)
					{
						if (deadConnections == null)
							deadConnections = new List<NetConnection>();
						deadConnections.Add(conn);
						continue;
					}

					conn.Heartbeat(now);
				}

				if (deadConnections != null)
				{
					foreach (NetConnection conn in deadConnections)
					{
						m_connections.Remove(conn);
						m_connectionLookup.Remove(conn.RemoteEndpoint);
					}
				}
			}
		}

		internal override NetConnection GetConnection(IPEndPoint remoteEndpoint)
		{
			NetConnection retval;
			if (m_connectionLookup.TryGetValue(remoteEndpoint, out retval))
				return retval;
			return null;
		}

		internal override void HandleReceivedMessage(NetMessage message, IPEndPoint senderEndpoint)
		{
			double now = NetTime.Now;

			int payLen = message.m_data.LengthBytes;

			if (message.m_sender == null)
			{
				// not a connected sender; only allow System messages
				if (message.m_type != NetMessageLibraryType.System)
				{
					// TODO: Notify application?
					LogWrite("Rejecting non-system message from unconnected source: " + message);
					return;
				}

				// read type of system message
				NetSystemType sysType = (NetSystemType)message.m_data.ReadByte();
				switch (sysType)
				{
					case NetSystemType.Connect:
						// check app ident
						if (payLen < 4)
						{
							// TODO: Notify application?
							LogWrite("Malformed Connect message received from " + senderEndpoint);
							return;
						}
						string appIdent = message.m_data.ReadString();
						if (appIdent != m_config.ApplicationIdentifier)
						{
							// TODO: Notify application?
							LogWrite("Connect for different application identification received: " + appIdent);
							return;
						}

						int bytesReadSoFar = (message.m_data.Position / 8);
						int hailLen = message.m_data.LengthBytes - bytesReadSoFar;
						byte[] hailData = null;
						if (hailLen > 0)
						{
							hailData = new byte[hailLen];
							Buffer.BlockCopy(message.m_data.Data, bytesReadSoFar, hailData, 0, hailLen);
						}

						if (m_connections.Count >= m_config.m_maxConnections)
						{
							// TODO: Notify application?
							LogWrite("Server full; rejecting connect from " + senderEndpoint);
							return;
						}

						//
						// TODO: ConnectionRequest the application
						//

						// Add connection
						LogWrite("New connection: " + senderEndpoint);
						NetConnection conn = new NetConnection(this, senderEndpoint, hailData);
						lock (m_connections)
							m_connections.Add(conn);
						m_connectionLookup.Add(senderEndpoint, conn);
						conn.HandleSystemMessage(message, now);

						break;
					case NetSystemType.ConnectionEstablished:
						// TODO: Notify application?
						LogWrite("Connection established received from non-connection!");
						return;
					case NetSystemType.Discovery:
						// check app ident
						if (payLen < 5)
						{
							// TODO: Notify application?
							LogWrite("Malformed Discovery message received");
							return;
						}
						string appIdent2 = message.m_data.ReadString();
						if (appIdent2 != m_config.ApplicationIdentifier)
						{
							// TODO: Notify application?
							LogWrite("Discovery for different application identification received: " + appIdent2);
							return;
						}

						// send discovery response
						SendDiscoveryResponse(senderEndpoint);
						break;
					default:
						// TODO: Notify application?
						LogWrite("Undefined behaviour for " + this + " receiving system type " + sysType + ": " + message + " from unconnected source");
						break;
				}
				// done
				return;
			}

			// ok, we have a sender
			if (message.m_type == NetMessageLibraryType.Acknowledge)
			{
				message.m_sender.HandleAckMessage(message);
				return;
			}

			if (message.m_type == NetMessageLibraryType.System)
			{
				// handle system messages from connected source
				if (payLen < 1)
				{
					// TODO: Notify application?
					LogWrite("Received malformed system message; payload length less than 1 byte");
					return;
				}
				NetSystemType sysType = (NetSystemType)message.m_data.ReadByte();
				switch (sysType)
				{
					case NetSystemType.Connect:
					case NetSystemType.ConnectionEstablished:
					case NetSystemType.Ping:
					case NetSystemType.Pong:
					case NetSystemType.Disconnect:
						message.m_sender.HandleSystemMessage(message, now);
						break;
					case NetSystemType.ConnectResponse:
						if (m_allowOutgoingConnections)
							message.m_sender.HandleSystemMessage(message, now);
						else
							LogWrite("Undefined behaviour for server and system type " + sysType);
						break;
					case NetSystemType.Discovery:
						// Allow discovery even if connected
						// check app ident
						if (payLen < 5)
						{
							// TODO: Notify application?
							LogWrite("Malformed Discovery message received");
							return;
						}
						string appIdent2 = message.m_data.ReadString();
						if (appIdent2 != m_config.ApplicationIdentifier)
						{
							// TODO: Notify application?
							LogWrite("Discovery for different application identification received: " + appIdent2);
							return;
						}

						// send discovery response
						SendDiscoveryResponse(senderEndpoint);
						break;
					default:
						// TODO: Notify application?
						LogWrite("Undefined behaviour for server and system type " + sysType);
						break;
				}
				return;
			}

			if (message.m_type == NetMessageLibraryType.UserFragmented)
				throw new NotImplementedException();

			// Must be user message at this point
			Debug.Assert(message.m_type == NetMessageLibraryType.User);

			message.m_sender.HandleUserMessage(message);
		}

		/*
		/// <summary>
		/// Read any received message in any connection queue
		/// </summary>
		public NetBuffer ReadMessage(out NetConnection sender)
		{
			if (m_receivedMessages.Count < 1)
			{
				sender = null;
				return null;
			}

			NetMessage msg = m_receivedMessages.Dequeue();
			sender = msg.m_sender;

			NetBuffer retval = msg.m_data;
			msg.m_data = null;
			m_messagePool.Push(msg);

			Debug.Assert(retval.Position == 0);

			return retval;
		}
		*/

		/// <summary>
		/// Read any received message in any connection queue
		/// </summary>
		public bool ReadMessage(NetBuffer intoBuffer, List<NetConnection> onlyFor, bool includeNullConnectionMessages, out NetMessageType type, out NetConnection sender)
		{
			NetMessage msg = null;
			lock (m_receivedMessages)
			{
				int sz = m_receivedMessages.Count;
				for (int i = 0; i < sz; i++)
				{
					msg = m_receivedMessages.Peek(i);
					if (msg != null)
					{
						if ((msg.m_sender == null && includeNullConnectionMessages) ||
							onlyFor.Contains(msg.m_sender))
						{
							m_receivedMessages.Dequeue(i);
							break;
						}
						msg = null;
					}
				}
			}

			if (msg == null)
			{
				sender = null;
				type = NetMessageType.None;
				return false;
			}

			sender = msg.m_sender;

			// recycle NetMessage object
			NetBuffer content = msg.m_data;
			msg.m_data = null;
			type = msg.m_msgType;

			lock (m_lockedMessagePool)
				m_lockedMessagePool.Enqueue(msg);

			Debug.Assert(content.Position == 0);

			// swap content of buffers
			byte[] tmp = intoBuffer.Data;
			intoBuffer.Data = content.Data;
			content.Data = tmp;

			// set correct values for returning value (ignore the other, it's being recycled anyway)
			intoBuffer.m_bitLength = content.m_bitLength;
			intoBuffer.m_readPosition = 0;

			// recycle NetBuffer object (incl. old intoBuffer byte array)
			content.m_refCount = 0;

			lock (m_lockedBufferPool)
				m_lockedBufferPool.Enqueue(content);

			return true;
		}

		/// <summary>
		/// Read any received message in any connection queue
		/// </summary>
		public bool ReadMessage(NetBuffer intoBuffer, out NetMessageType type, out NetConnection sender)
		{
			NetMessage msg;
			lock(m_receivedMessages)
				msg = m_receivedMessages.Dequeue();

			if (msg == null)
			{
				sender = null;
				type = NetMessageType.None;
				return false;
			}

			sender = msg.m_sender;

			// recycle NetMessage object
			NetBuffer content = msg.m_data;
			msg.m_data = null;
			type = msg.m_msgType;

			lock(m_lockedMessagePool)
				m_lockedMessagePool.Enqueue(msg);

			Debug.Assert(content.Position == 0);

			// swap content of buffers
			byte[] tmp = intoBuffer.Data;
			intoBuffer.Data = content.Data;
			content.Data = tmp;

			// set correct values for returning value (ignore the other, it's being recycled anyway)
			intoBuffer.m_bitLength = content.m_bitLength;
			intoBuffer.m_readPosition = 0;

			// recycle NetBuffer object (incl. old intoBuffer byte array)
			content.m_refCount = 0;

			lock (m_lockedBufferPool)
				m_lockedBufferPool.Enqueue(content);

			return true;
		}

		/// <summary>
		/// Sends a message to a specific connection
		/// </summary>
		public void SendMessage(NetBuffer data, NetConnection recipient, NetChannel channel)
		{
			if (recipient == null)
				throw new ArgumentNullException("recipient");
			recipient.SendMessage(data, channel);
		}

		/// <summary>
		/// Sends a message to the specified connections
		/// </summary>
		public void SendMessage(NetBuffer data, IList<NetConnection> recipients, NetChannel channel)
		{
			if (recipients == null)
				throw new ArgumentNullException("recipients");

			foreach (NetConnection recipient in recipients)
				recipient.SendMessage(data, channel);
		}

		/// <summary>
		/// Sends a message to all connections to this server
		/// </summary>
		public void SendToAll(NetBuffer data, NetChannel channel)
		{
			lock (m_connections)
			{
				foreach (NetConnection conn in m_connections)
				{
					if (conn.Status == NetConnectionStatus.Connected)
						conn.SendMessage(data, channel);
				}
			}
		}

		/// <summary>
		/// Sends a message to all connections to this server, except 'exclude'
		/// </summary>
		public void SendToAll(NetBuffer data, NetChannel channel, NetConnection exclude)
		{
			lock (m_connections)
			{
				foreach (NetConnection conn in m_connections)
				{
					if (conn.Status == NetConnectionStatus.Connected && conn != exclude)
						conn.SendMessage(data, channel);
				}
			}
		}

		internal override void HandleConnectionForciblyClosed(NetConnection connection, SocketException sex)
		{
			if (connection != null)
				connection.Disconnect("Connection forcibly closed", 0, false);
			return;
		}

		private void SendDiscoveryResponse(IPEndPoint senderEndpoint)
		{
			m_scratchBuffer.Reset();

			SendSingleUnreliableSystemMessage(
				NetSystemType.DiscoveryResponse,
				m_scratchBuffer,
				senderEndpoint
			);
		}

		protected override void PerformShutdown(string reason)
		{
			foreach (NetConnection conn in m_connections)
				if (conn.m_status != NetConnectionStatus.Disconnected)
					conn.Disconnect(reason, 0, true);
			base.PerformShutdown(reason);
		}
	}
}
