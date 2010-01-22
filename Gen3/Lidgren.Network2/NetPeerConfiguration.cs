using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Diagnostics;

namespace Lidgren.Network2
{
	/// <summary>
	/// Immutable after NetPeer has been initialized
	/// </summary>
	public sealed class NetPeerConfiguration
	{
		private const string c_isLockedMessage = "You may not alter the NetPeerConfiguration after the NetPeer has been initialized!";

		private bool m_isLocked;
		internal bool m_acceptIncomingConnections;
		internal string m_appIdentifier;
		internal IPAddress m_localAddress;
		internal int m_port;
		internal int m_receiveBufferSize, m_sendBufferSize;
		internal int m_defaultOutgoingMessageCapacity;
		internal int m_maximumTransmissionUnit;
		internal float m_keepAliveDelay;
		internal float m_connectionTimeOut;
		internal int m_maximumConnections;
		internal float m_latencyCalculationWindowSize;
		private bool[] m_disabledTypes;

		// bad network simulation
		internal float m_loss;
		internal float m_minimumOneWayLatency;
		internal float m_randomOneWayLatency;

		public NetPeerConfiguration(string appIdentifier)
		{
			if (string.IsNullOrEmpty(appIdentifier))
				throw new NetException("App identifier must be a string of at least one characters length");
			m_appIdentifier = appIdentifier;

			// defaults
			m_isLocked = false;
			m_acceptIncomingConnections = true;
			m_localAddress = IPAddress.Any;
			m_port = 0;
			m_receiveBufferSize = 131071;
			m_sendBufferSize = 131071;
			m_maximumTransmissionUnit = 1400;
			m_keepAliveDelay = 3;
			m_connectionTimeOut = 25;
			m_maximumConnections = 8;
			m_latencyCalculationWindowSize = 1.0f;
			m_defaultOutgoingMessageCapacity = 8;

			m_loss = 0.0f;
			m_minimumOneWayLatency = 0.0f;
			m_randomOneWayLatency = 0.0f;

			// default disabled types
			m_disabledTypes = new bool[Enum.GetNames(typeof(NetIncomingMessageType)).Length];
			m_disabledTypes[(int)NetIncomingMessageType.ConnectionApproval] = true;
			m_disabledTypes[(int)NetIncomingMessageType.UnconnectedData] = true;
			m_disabledTypes[(int)NetIncomingMessageType.VerboseDebugMessage] = true;
		}

		public void Lock()
		{
			m_isLocked = true;
		}

#if DEBUG
		/// <summary>
		/// Gets or sets the simulated amount of sent packets lost from 0.0f to 1.0f
		/// </summary>
		public float SimulatedLoss
		{
			get { return m_loss; }
			set { m_loss = value; }
		}

		/// <summary>
		/// Gets or sets the minimum simulated amount of one way latency for sent packets in seconds
		/// </summary>
		public float SimulatedMinimumLatency
		{
			get { return m_minimumOneWayLatency; }
			set { m_minimumOneWayLatency = value; }
		}

		/// <summary>
		/// Gets or sets the simulated added random amount of one way latency for sent packets in seconds
		/// </summary>
		public float SimulatedRandomLatency
		{
			get { return m_randomOneWayLatency; }
			set { m_randomOneWayLatency = value; }
		}

		/// <summary>
		/// Gets the average simulated one way latency in seconds
		/// </summary>
		public float SimulatedAverageLatency
		{
			get { return m_minimumOneWayLatency + (m_randomOneWayLatency * 0.5f); }
		}

#endif

		/// <summary>
		/// Gets or sets the identifier of this application; the library can only connect to matching app identifier peers
		/// </summary>
		public string AppIdentifier
		{
			get { return m_appIdentifier; }
		}

		/// <summary>
		/// Enables receiving of the specified type of message
		/// </summary>
		public void EnableMessageType(NetIncomingMessageType tp)
		{
			m_disabledTypes[(int)tp] = false;
		}

		/// <summary>
		/// Disables receiving of the specified type of message
		/// </summary>
		public void DisableMessageType(NetIncomingMessageType tp)
		{
			m_disabledTypes[(int)tp] = true;
		}
		
		/// <summary>
		/// Enables or disables receiving of the specified type of message
		/// </summary>
		public void SetMessageTypeEnabled(NetIncomingMessageType tp, bool enabled)
		{
			m_disabledTypes[(int)tp] = !enabled;
		}

		/// <summary>
		/// Gets if receiving of the specified type of message is enabled
		/// </summary>
		public bool IsMessageTypeEnabled(NetIncomingMessageType tp)
		{
			return !m_disabledTypes[(int)tp];
		}

		/// <summary>
		/// Gets or sets the maximum amount of bytes to send in a single packet
		/// </summary>
		public int MaximumTransmissionUnit
		{
			get { return m_maximumTransmissionUnit; }
			set { m_maximumTransmissionUnit = value; }
		}

		/// <summary>
		/// Gets or sets the maximum amount of connections this peer can hold. Cannot be changed once NetPeer is initialized.
		/// </summary>
		public int MaximumConnections
		{
			get { return m_maximumTransmissionUnit; }
			set
			{
				if (m_isLocked)
					throw new NetException(c_isLockedMessage);
				m_maximumTransmissionUnit = value;
			}
		}

		/// <summary>
		/// Gets or sets if the NetPeer should accept incoming connections
		/// </summary>
		public bool AcceptIncomingConnections
		{
			get { return m_acceptIncomingConnections; }
			set { m_acceptIncomingConnections = value; }
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
		/// Gets or sets the local ip address to bind to. Defaults to IPAddress.Any. Cannot be changed once NetPeer is initialized.
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
		/// Gets or sets the local port to bind to. Defaults to 0. Cannot be changed once NetPeer is initialized.
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
		/// Gets or sets the size in bytes of the receiving buffer. Defaults to 131071 bytes. Cannot be changed once NetPeer is initialized.
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
		/// Gets or sets the size in bytes of the sending buffer. Defaults to 131071 bytes. Cannot be changed once NetPeer is initialized.
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

		/// <summary>
		/// Gets or sets the number of seconds of inactivity before sending a dummy keepalive packet
		/// </summary>
		public float KeepAliveDelay
		{
			get { return m_keepAliveDelay; }
			set
			{
				m_keepAliveDelay = value;
			}
		}

		/// <summary>
		/// Gets or sets the number of seconds during which the average roundtrip time should be calculated
		/// </summary>
		public float LatencyCalculationWindowSize
		{
			get { return m_latencyCalculationWindowSize; }
			set
			{
				if (m_isLocked)
					throw new NetException(c_isLockedMessage);
				m_latencyCalculationWindowSize = value;
			}
		}

		/// <summary>
		/// Gets or sets the number of seconds of non-response before disconnecting because of time out. Cannot be changed once NetPeer is initialized.
		/// </summary>
		public float ConnectionTimeOut
		{
			get { return m_connectionTimeOut; }
			set
			{
				if (m_isLocked)
					throw new NetException(c_isLockedMessage);
				m_connectionTimeOut = value;
			}
		}

	}
}
