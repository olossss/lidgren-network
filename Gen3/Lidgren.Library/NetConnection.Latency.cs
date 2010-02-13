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

		private float m_currentAvgRoundtrip = 0.75f; // large to avoid initial resends

		private byte m_lastSentPingNumber;
		private double m_pingSendTime;
		private double m_nextPing;

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
			m_currentAvgRoundtrip = roundtripTime;
			m_owner.LogDebug("Initializing avg rtt to " + NetTime.ToReadable(m_currentAvgRoundtrip));
			m_isPingInitialized = true;
			m_nextKeepAlive = now + (m_owner.m_configuration.KeepAliveDelay * 3);
			m_lastSendRespondedTo = now;
		}

		private void KeepAliveHeartbeat(double now)
		{
			if (m_status != NetConnectionStatus.Disconnected && m_status != NetConnectionStatus.None)
			{
				if (now > m_nextKeepAlive || m_numUnackedPackets > (m_owner.m_configuration.m_windowSize / 4))
				{
					// send dummy keepalive message; remote host will response with an ack 
					m_owner.LogVerbose("Sending keepalive; numunackedpackets: " + m_numUnackedPackets);

					NetOutgoingMessage keepalive = m_owner.CreateMessage(1);
					keepalive.m_type = NetMessageType.LibraryKeepAlive;
					EnqueueOutgoingMessage(keepalive, NetMessagePriority.High);

					m_nextKeepAlive = now + m_owner.m_configuration.KeepAliveDelay;
				}

				// timeout
				if (!m_disconnectRequested && now > m_lastSendRespondedTo + m_owner.m_configuration.m_connectionTimeOut)
					Disconnect("Timed out");

				// ping time?
				if (now > m_nextPing)
				{
					SendPing(now);
					m_nextPing = now + m_owner.Configuration.m_pingFrequency;
				}
			}
		}

		internal void SendPing(double now)
		{
			NetOutgoingMessage ping = m_owner.CreateMessage(1);
			ping.m_type = NetMessageType.LibraryPing;

			m_lastSentPingNumber++;
			ping.Write(m_lastSentPingNumber);

			m_pingSendTime = now;

			EnqueueOutgoingMessage(ping, NetMessagePriority.High);
		}


		internal void HandleIncomingPing(byte pingNumber)
		{
			// send pong
			NetOutgoingMessage pong = m_owner.CreateMessage(1);
			pong.m_type = NetMessageType.LibraryPong;
			pong.Write(pingNumber);
			EnqueueOutgoingMessage(pong, NetMessagePriority.High);
		}

		internal void HandleIncomingPong(double now, byte pingNumber)
		{
			// verify it´s the correct ping number
			if (pingNumber != m_lastSentPingNumber)
			{
				m_owner.LogDebug("Received wrong pong number");
				return;
			}

			double rtt = now - m_pingSendTime;
			UpdateLatency(now, (float)rtt);
		}

		internal void UpdateLatency(double now, float rtt)
		{
			// calculate avg rtt 
			m_currentAvgRoundtrip = (m_currentAvgRoundtrip * 0.75f) + (rtt * 0.25f);

			m_owner.LogDebug("Found RTT: " + NetTime.ToReadable(rtt) + " new average: " + NetTime.ToReadable(m_currentAvgRoundtrip));
		}
	}
}
