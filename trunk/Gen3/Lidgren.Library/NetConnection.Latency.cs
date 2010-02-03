using System;
using System.Threading;

namespace Lidgren.Network
{
	public partial class NetConnection
	{
		//
		// Connection keepalive and latency calculation
		//
		private bool m_isPingInitialized;

		private double m_latencyWindowStart;
		private float m_latencyWindowSize;
		private float m_latencySum;
		private int m_latencyCount;
		private float m_currentAvgRoundtrip = 0.75f; // large to avoid initial resends

		private double m_nextKeepAlive;
		internal double m_lastSendRespondedTo; // timestamp when data was sent, for which a response has been received

		/// <summary>
		/// Gets the current average roundtrip time
		/// </summary>
		public float AverageRoundtripTime { get { return (float)m_currentAvgRoundtrip; } }

		private void UpdateLastSendRespondedTo(double timestamp)
		{
			m_lastSendRespondedTo = timestamp;
			m_nextKeepAlive = timestamp + m_owner.m_configuration.KeepAliveDelay;
		}

		private void InitializeLatency(float roundtripTime)
		{
			double now = NetTime.Now;
			if (roundtripTime < 0.0f)
				roundtripTime = 0.0f;
			if (roundtripTime > 4.0f)
				roundtripTime = 4.0f; // unlikely high
			m_latencyWindowStart = now;
			m_latencySum = roundtripTime;
			m_latencyCount = 1;
			m_currentAvgRoundtrip = roundtripTime;
			m_owner.LogDebug("Initializing avg rtt to " + NetTime.ToReadable(m_currentAvgRoundtrip));
			m_isPingInitialized = true;
			m_nextKeepAlive = NetTime.Now + (m_owner.m_configuration.KeepAliveDelay * 3);
		}

		private void KeepAliveHeartbeat(double now)
		{
			if (m_status != NetConnectionStatus.Disconnected)
			{
				if (now > m_nextKeepAlive || m_numUnackedPackets > NetPeer.WINDOW_SIZE / 2)
				{
					// send dummy keepalive message; remote host will response with an ack 
					m_owner.LogVerbose("Sending keepalive/explicit ack");

					NetOutgoingMessage ping = m_owner.CreateMessage(1);
					ping.m_type = NetMessageType.LibraryKeepAlive;
					EnqueueOutgoingMessage(ping, NetMessagePriority.High);

					m_nextKeepAlive = now + m_owner.m_configuration.KeepAliveDelay;
				}

				// timeout
				if (!m_disconnectRequested && now > m_lastSendRespondedTo + m_owner.m_configuration.ConnectionTimeOut)
					Disconnect("Timed out");
			}
		}

		internal void UpdateLatency(double now, float rt)
		{
			m_owner.LogVerbose("Found RTT: " + NetTime.ToReadable(m_currentAvgRoundtrip));
			if (now > m_latencyWindowStart + m_latencyWindowSize)
			{
				// calculate avg rt 
				if (m_latencyCount > 0)
				{
					m_currentAvgRoundtrip = (m_currentAvgRoundtrip + (m_latencySum / m_latencyCount)) * 0.5f;
					m_owner.LogVerbose("Updating avg rtt to " + NetTime.ToReadable(m_currentAvgRoundtrip) + " using " + m_latencyCount + " samples");
				}

				m_latencyWindowStart = now;
				m_latencySum = rt;
				m_latencyCount = 1;
			}
			else
			{
				m_latencyCount++;
				m_latencySum += rt;
			}
		}
	}
}
