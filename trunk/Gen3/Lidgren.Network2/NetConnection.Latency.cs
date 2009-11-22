using System;
using System.Threading;

namespace Lidgren.Network2
{
	public partial class NetConnection
	{
		internal const int c_maxPingNumber = ushort.MaxValue;

		//
		// Connection keepalive and latency calculation
		//

		private ushort m_lastPingNumber;
		private double m_lastPingSent;
		private bool m_isPingInitialized;
		private double[] m_latencyHistory = new double[3];
		private double m_currentAvgRoundtrip = 0.75f; // large to avoid initial resends
		private double m_lastSendRespondedTo; // timestamp when data was sent, for which a response has been received

		/// <summary>
		/// Gets the current average roundtrip time
		/// </summary>
		public float AverageRoundtripTime { get { return (float)m_currentAvgRoundtrip; } }

		private void SetInitialAveragePing(double roundtripTime)
		{
			if (roundtripTime < 0.0f)
				roundtripTime = 0.0;
			if (roundtripTime > 3.0)
				roundtripTime = 3.0; // unlikely high

			m_latencyHistory[2] = roundtripTime * 1.2 + 0.01; // overestimate
			m_latencyHistory[1] = roundtripTime * 1.1 + 0.005; // overestimate
			m_latencyHistory[0] = roundtripTime; // overestimate
			m_owner.LogDebug("Initializing avg rt to " + (int)(roundtripTime * 1000) + " ms");
		}

		private void KeepAliveHeartbeat(double now)
		{
			// time to send a ping?
			if (m_status != NetConnectionStatus.Disconnected)
			{
				if (now > m_lastPingSent + m_owner.m_configuration.PingFrequency)
					SendPing(now);

				if (now > m_lastSendRespondedTo + m_owner.m_configuration.ConnectionTimeOut)
					Disconnect("Timed out");
			}
		}

		private void SendPing(double now)
		{
			NetOutgoingMessage ping = m_owner.CreateMessage(2);
			ping.m_type = NetMessageType.LibraryPing;
			ping.Write(m_lastPingNumber);

			m_lastPingNumber++;
			m_lastPingSent = now;

			SendMessage(ping, NetMessagePriority.High);
		}

		internal void HandlePing(ushort nr)
		{
			// send matching pong
			NetOutgoingMessage reply = m_owner.CreateMessage(2);
			reply.m_type = NetMessageType.LibraryPong;
			reply.Write(nr);
			SendMessage(reply, NetMessagePriority.High);
		}

		internal void HandlePong(double now, ushort nr)
		{
			if (nr != m_lastPingNumber)
			{
				m_owner.LogDebug("Received wrong order pong number (" + nr + ", expecting " + m_lastPingNumber);
				return;
			}

			m_lastSendRespondedTo = m_lastPingSent;

			double roundtripTime = now - m_lastPingSent;

			if (m_isPingInitialized == false)
			{
				SetInitialAveragePing(roundtripTime);
				return;
			}

			// calculate new average roundtrip time
			m_latencyHistory[2] = m_latencyHistory[1];
			m_latencyHistory[1] = m_latencyHistory[0];
			m_latencyHistory[0] = roundtripTime;
			m_currentAvgRoundtrip = ((roundtripTime * 3) + (m_latencyHistory[1] * 2) + m_latencyHistory[2]) / 6.0;

			m_owner.LogDebug("Received pong; roundtrip time is " + (int)(roundtripTime * 1000) + " ms");
		}
	}
}
