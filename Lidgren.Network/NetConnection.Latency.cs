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
using System.Diagnostics;

namespace Lidgren.Network
{
	public sealed partial class NetConnection
	{
		private double m_lastSentPing;
		private double[] m_latencyHistory = new double[3];
		private double m_currentAvgRoundtrip = 0.5f; // large to avoid initial resends
		private float m_ackMaxDelayTime = 0.0f;
		private byte[] m_pingPongScratchPad = new byte[2];

		// Local time = Remote time + m_remoteOffset
		internal int m_remoteTimeOffset;

		/// <summary>
		/// Gets the current average roundtrip time
		/// </summary>
		public float AverageRoundtripTime { get { return (float)m_currentAvgRoundtrip; } }

		private void SetInitialAveragePing(double roundtripTime)
		{
			if (roundtripTime < 0.0f)
				roundtripTime = 0.0;
			if (roundtripTime > 2.0)
				roundtripTime = 2.0; // sounds awfully high?

			roundtripTime -= 0.1f;
			if (roundtripTime < 0.005)
				roundtripTime = 0.005; // minimum 5 ms

			m_latencyHistory[2] = roundtripTime * 1.25 + 0.01; // overestimate
			m_latencyHistory[1] = roundtripTime * 1.15 + 0.005; // overestimate
			m_latencyHistory[0] = roundtripTime * 1.1; // overestimate
			ReceivedPong(roundtripTime, null);
			m_owner.LogWrite("Initializing avg rt to " + (int)(roundtripTime * 1000) + " ms");
		}

		private void CheckPing(double now)
		{
			if (m_status == NetConnectionStatus.Connected && now - m_lastSentPing > m_owner.Configuration.PingFrequency)
			{
				// check for timeout
				if (now - m_lastSentPing > m_owner.Configuration.TimeoutDelay)
				{
					// Time out!
					Disconnect("Connection timed out", 1.0f, true);
					return;
				}

				// send ping
				SendPingPong(now, NetSystemType.Ping);
			}

		}

		private void SendPingPong(double now, NetSystemType pingOrPong)
		{
			Debug.Assert(pingOrPong == NetSystemType.Ping || pingOrPong == NetSystemType.Pong);

			ushort nowEnc = NetTime.Encoded(now);
			NetBuffer buffer = m_owner.m_scratchBuffer;
			buffer.Reset();
			buffer.Write(nowEnc);
			m_owner.SendSingleUnreliableSystemMessage(
				pingOrPong,
				buffer,
				m_remoteEndPoint
			);
			m_lastSentPing = now;
		}

		private void ReceivedPong(double rtSeconds, NetMessage pong)
		{
			double now = NetTime.Now;
			if (pong != null)
			{
				ushort remote = pong.m_data.ReadUInt16();
				ushort local = NetTime.Encoded(now);
				int diff = local - remote - (int)(rtSeconds * 1000.0);
				if (diff < 0)
					diff += ushort.MaxValue;
				m_remoteTimeOffset = diff; // TODO: slowly go towards? (m_remoteTimeOffset + diff) / 2;
				//LogWrite("GOT PONG; remote is " + remote + " local is " + local + " = offset " + m_remoteTimeOffset);
			}

			m_latencyHistory[2] = m_latencyHistory[1];
			m_latencyHistory[1] = m_latencyHistory[0];
			m_latencyHistory[0] = rtSeconds;
			m_currentAvgRoundtrip = ((rtSeconds * 3) + (m_latencyHistory[1] * 2) + m_latencyHistory[2]) / 6.0;

			m_ackMaxDelayTime = (float)(
				m_owner.m_config.m_maxAckWithholdTime * rtSeconds * m_owner.m_config.m_resendTimeMultiplier
			);
		}
	}
}
