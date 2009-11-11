using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace Lidgren.Network2
{
	/// <summary>
	/// Immutable after NetPeer has been initialized
	/// </summary>
	public sealed class NetPeerConfiguration
	{
		private const string c_isLockedMessage = "You may not alter the NetPeerConfiguration after the NetPeer has been initialized!";

		private bool m_isLocked;
		private IPAddress m_localAddress;
		private int m_port;
		private int m_receiveBufferSize, m_sendBufferSize;

		public NetPeerConfiguration()
		{
			// defaults
			m_isLocked = false;
			m_localAddress = IPAddress.Any;
			m_port = 0;
			m_receiveBufferSize = 131071;
			m_sendBufferSize = 131071;
		}

		public void Lock()
		{
			m_isLocked = true;
		}

		/// <summary>
		/// The local ip address to bind to. Defaults to IPAddress.Any
		/// </summary>
		public IPAddress LocalAddress
		{
			get { return m_localAddress; }
			set
			{
				if (m_isLocked)
					throw new NetException(c_isLockedMessage);
				m_localAddress = value;
			}
		}

		/// <summary>
		/// The local port to bind to. Defaults to 0
		/// </summary>
		public int Port
		{
			get { return m_port; }
			set
			{
				if (m_isLocked)
					throw new NetException(c_isLockedMessage);
				m_port = value;
			}
		}

		/// <summary>
		/// Size in bytes of the receiving buffer. Defaults to 131071 bytes.
		/// </summary>
		public int ReceiveBufferSize
		{
			get { return m_receiveBufferSize; }
			set
			{
				if (m_isLocked)
					throw new NetException(c_isLockedMessage);
				m_receiveBufferSize = value;
			}
		}

		/// <summary>
		/// Size in bytes of the sending buffer. Defaults to 131071 bytes.
		/// </summary>
		public int SendBufferSize
		{
			get { return m_sendBufferSize; }
			set
			{
				if (m_isLocked)
					throw new NetException(c_isLockedMessage);
				m_sendBufferSize = value;
			}
		}

	}
}
