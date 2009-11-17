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
		private string m_appIdentifier;
		private IPAddress m_localAddress;
		private int m_port;
		private int m_receiveBufferSize, m_sendBufferSize;
		private int m_defaultOutgoingMessageCapacity;
		private int m_maximumTransmissionUnit;

		public NetPeerConfiguration(string appIdentifier)
		{
			m_appIdentifier = appIdentifier;

			// defaults
			m_isLocked = false;
			m_localAddress = IPAddress.Any;
			m_port = 0;
			m_receiveBufferSize = 131071;
			m_sendBufferSize = 131071;
			m_maximumTransmissionUnit = 1400;
		}

		public void Lock()
		{
			m_isLocked = true;
		}

		/// <summary>
		/// Gets or sets the identifier of this application; the library can only connect to matching app identifier peers
		/// </summary>
		public string AppIdentifier
		{
			get { return m_appIdentifier; }
		}

		/// <summary>
		/// Gets or sets the maximum amount of bytes to send in a packet
		/// </summary>
		public int MaximumTransmissionUnit
		{
			get { return m_maximumTransmissionUnit; }
			set { m_maximumTransmissionUnit = value; }
		}

		/// <summary>
		/// Gets or sets the default capacity in bytes when NetPeer.CreateMessage() is called without argument
		/// </summary>
		public int DefaultOutgoingMessageCapacity
		{
			get { return m_defaultOutgoingMessageCapacity; }
			set { m_defaultOutgoingMessageCapacity = value; }
		}

		/// <summary>
		/// Gets or sets the local ip address to bind to. Defaults to IPAddress.Any
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
		/// Gets or sets the local port to bind to. Defaults to 0
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
		/// Gets or sets the size in bytes of the receiving buffer. Defaults to 131071 bytes.
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
		/// Gets or sets the size in bytes of the sending buffer. Defaults to 131071 bytes.
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
