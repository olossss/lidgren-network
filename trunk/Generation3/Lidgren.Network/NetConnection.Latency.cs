/* Copyright (c) 2010 Michael Lidgren

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

namespace Lidgren.Network
{
	public partial class NetConnection
	{
		//
		// Connection keepalive and latency calculation
		//
		private bool m_isPingInitialized;
		private float m_averageRoundtripTime = 0.05f;
		private byte m_lastSentPingNumber;
		private double m_lastPingSendTime;
		private double m_nextPing;
		private double m_nextKeepAlive;
		private double m_lastSendRespondedTo;

		public float AverageRoundtripTime { get { return m_averageRoundtripTime; } }

		internal void UpdateLatency(float rtt)
		{
			if (!m_isPingInitialized)
			{
				m_isPingInitialized = true;
				m_averageRoundtripTime = rtt;
				m_nextPing = NetTime.Now + (m_owner.m_configuration.m_pingFrequency * 2);
			}
			else
			{
				m_averageRoundtripTime = (m_averageRoundtripTime * 0.75f) + (rtt * 0.25f);
				m_owner.LogDebug("New average roundtrip time: " + m_averageRoundtripTime);
			}
		}

		internal void HandleIncomingPing(byte pingNumber)
		{
			// send pong
			NetOutgoingMessage pong = m_owner.CreateMessage(1);
			pong.m_type = NetMessageType.Library;
			pong.m_libType = NetMessageLibraryType.Pong;
			pong.Write(pingNumber);
			EnqueueOutgoingMessage(pong);
		}

		internal void HandleIncomingPong(double now, byte pingNumber)
		{
			// verify it´s the correct ping number
			if (pingNumber != m_lastSentPingNumber)
			{
				m_owner.LogDebug("Received wrong pong number");
				return;
			}

			m_lastHeardFrom = now;
			m_lastSendRespondedTo = m_lastPingSendTime;

			m_nextKeepAlive = now + m_owner.m_configuration.m_keepAliveDelay;

			UpdateLatency((float)(now - m_lastPingSendTime));
		}

		internal void KeepAliveHeartbeat(double now)
		{
			// do keepalive and latency pings
			if (m_status == NetConnectionStatus.Disconnected || m_status == NetConnectionStatus.None)
				return;

			if (now > m_nextKeepAlive)
			{
				// send keepalive message
				m_owner.LogVerbose("Sending keepalive");

				NetOutgoingMessage keepalive = m_owner.CreateMessage(1);
				keepalive.m_type = NetMessageType.Library;
				keepalive.m_libType = NetMessageLibraryType.KeepAlive;
				EnqueueOutgoingMessage(keepalive);

				m_nextKeepAlive = now + m_owner.m_configuration.KeepAliveDelay;
			}

			// timeout
			if (now > m_lastSendRespondedTo + m_owner.m_configuration.m_connectionTimeOut)
				Disconnect("Timed out");

			// ping time?
			if (now > m_nextPing)
			{
				//
				// send ping
				//
				m_lastSentPingNumber++;

				NetOutgoingMessage ping = m_owner.CreateMessage(1);
				ping.m_type = NetMessageType.Library;
				ping.m_libType = NetMessageLibraryType.Ping;
				ping.Write(m_lastSentPingNumber);
				EnqueueOutgoingMessage(ping);

				m_lastPingSendTime = now;
				m_nextPing = now + m_owner.Configuration.m_pingFrequency;
			}
		}
	}
}
