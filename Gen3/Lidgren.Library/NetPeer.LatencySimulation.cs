using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Lidgren.Network
{
	public partial class NetPeer
	{

#if DEBUG
		private List<DelayedPacket> m_delayedPackets = new List<DelayedPacket>();

		private class DelayedPacket
		{
			public byte[] Data;
			public double DelayedUntil;
			public IPEndPoint Target;
		}

		internal void SendPacket(int numBytes, IPEndPoint target)
		{
			byte packetNumber = m_sendBuffer[0];

			// simulate loss
			float loss = m_configuration.m_loss;
			if (loss > 0.0f)
			{
				if (NetRandom.Instance.Chance(m_configuration.m_loss))
				{
					LogVerbose("Sending packet #" + packetNumber + " (" + numBytes + " bytes) - SIMULATED LOST!");
					return; // packet "lost"
				}
			}

			// simulate latency
			float m = m_configuration.m_minimumOneWayLatency;
			float r = m_configuration.m_randomOneWayLatency;
			if (m == 0.0f && r == 0.0f)
			{
				// no latency simulation
				LogVerbose("Sending packet #" + packetNumber + " (" + numBytes + " bytes)");
				ActuallySendPacket(m_sendBuffer, numBytes, target);
				return;
			}

			int num = 1;
			if (m_configuration.m_duplicates > 0.0f && NetRandom.Instance.Chance(m_configuration.m_duplicates))
				num++;

			float delay = 0;
			for (int i = 0; i < num; i++)
			{
				delay = m_configuration.m_minimumOneWayLatency + (NetRandom.Instance.NextFloat() * m_configuration.m_randomOneWayLatency);

				// Enqueue delayed packet
				DelayedPacket p = new DelayedPacket();
				p.Target = target;
				p.Data = new byte[numBytes];
				Buffer.BlockCopy(m_sendBuffer, 0, p.Data, 0, numBytes);
				p.DelayedUntil = NetTime.Now + delay;

				m_delayedPackets.Add(p);
			}

			LogVerbose("Sending packet #" + packetNumber + " (" + numBytes + " bytes) - Delayed " + NetTime.ToReadable(delay));
		}

		private void SendDelayedPackets()
		{
			if (m_delayedPackets.Count <= 0)
				return;

			double now = NetTime.Now;

			RestartDelaySending:
			foreach (DelayedPacket p in m_delayedPackets)
			{
				if (now > p.DelayedUntil)
				{
					ActuallySendPacket(p.Data, p.Data.Length, p.Target);
					m_delayedPackets.Remove(p);
					goto RestartDelaySending;
				}
			}
		}

		internal void ActuallySendPacket(byte[] data, int numBytes, IPEndPoint target)
		{
			try
			{
				// TODO: Use SendToAsync()?
				int bytesSent = m_socket.SendTo(data, 0, numBytes, SocketFlags.None, target);
				if (numBytes != bytesSent)
					LogWarning("Failed to send the full " + numBytes + "; only " + bytesSent + " bytes sent in packet!");

				if (bytesSent >= 0)
				{
					m_statistics.m_sentPackets++;
					m_statistics.m_sentBytes += bytesSent;
				}
			}
			catch (Exception ex)
			{
				LogError("Failed to send packet: " + ex);
			}
		}
#else
		//
		// Release - just send the packet straight away
		//
		internal void SendPacket(int numBytes, IPEndPoint target)
		{
			try
			{
				// TODO: Use SendToAsync()?
				int bytesSent = m_socket.SendTo(m_sendBuffer, 0, numBytes, SocketFlags.None, target);
				if (numBytes != bytesSent)
					LogWarning("Failed to send the full " + numBytes + "; only " + bytesSent + " bytes sent in packet!");

				if (bytesSent >= 0)
				{
					m_statistics.m_sentPackets++;
					m_statistics.m_sentBytes += bytesSent;
				}
			}
			catch (Exception ex)
			{
				LogError("Failed to send packet: " + ex);
			}
		}
#endif
	}
}
