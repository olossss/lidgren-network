using System;

namespace Lidgren.Network2
{
	public partial class NetConnection
	{
		private byte m_lastPingNumber;
		private double m_lastPingSent;
		private bool m_isPingInitialized;
		private double[] m_latencyHistory = new double[3];
		private double m_currentAvgRoundtrip = 0.75f; // large to avoid initial resends

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

		internal void HandlePing(byte nr)
		{
			NetOutgoingMessage reply = m_owner.CreateMessage(2);
			reply.Write(nr);
			QueueOutgoing(reply, NetMessagePriority.High);
		}

		internal void HandlePong(double now, byte nr)
		{
			if (nr != m_lastPingNumber)
			{
				m_owner.LogDebug("Received wrong order pong number (" + nr + ", expecting " + m_lastPingNumber);
				return;
			}

			double roundtripTime = now - m_lastPingSent;

			if (m_isPingInitialized == false)
			{
				SetInitialAveragePing(roundtripTime);
				return;
			}

			m_latencyHistory[2] = m_latencyHistory[1];
			m_latencyHistory[1] = m_latencyHistory[0];
			m_latencyHistory[0] = roundtripTime;
			m_currentAvgRoundtrip = ((roundtripTime * 3) + (m_latencyHistory[1] * 2) + m_latencyHistory[2]) / 6.0;

			m_owner.LogDebug("Received pong; roundtrip time is " + (int)(roundtripTime * 1000) + " ms");
		}
	}
}
