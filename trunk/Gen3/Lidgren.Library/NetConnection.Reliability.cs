using System;
using System.Collections.Generic;
using System.Text;

namespace Lidgren.Network
{
	public partial class NetConnection
	{
		//
		// Selective repeat sliding window
		//
		internal int m_receiveWindowBase;
		internal int m_sendWindowBase;
		internal int m_sendNext;

		internal double[] m_sendTimes = new double[NetPeer.WINDOW_SIZE];
		internal int m_numUnackedPackets;

		internal uint EarlyArrivalBitMask;

		private void ResetSlidingWindow()
		{
			m_receiveWindowBase = 0;
			m_sendWindowBase = 0;
			m_sendNext = 0;
		}

		internal bool CanSend()
		{
			if ((m_sendWindowBase + NetPeer.WINDOW_SIZE) % NetPeer.NUM_SERIALS == m_sendNext)
				return false; // window full
			return true;
		}

		/// <summary>
		/// Returns true if a packet can be sent (and if it does, it must be sent!)
		/// </summary>
		internal void PrepareSend(double now)
		{
			byte[] buffer = m_owner.m_sendBuffer;

			m_sendTimes[m_sendNext % NetPeer.WINDOW_SIZE] = now;

			buffer[0] = (byte)m_sendNext;
			m_sendNext = (m_sendNext + 1) % NetPeer.NUM_SERIALS;
			m_numUnackedPackets = 0;

			buffer[1] = (byte)((m_receiveWindowBase - 1 + NetPeer.NUM_SERIALS) % NetPeer.NUM_SERIALS);
			buffer[2] = (byte)EarlyArrivalBitMask;
			buffer[3] = (byte)(EarlyArrivalBitMask >> 8);
			buffer[4] = (byte)(EarlyArrivalBitMask >> 16);
			buffer[5] = (byte)(EarlyArrivalBitMask >> 24);
		}

		/// <summary>
		/// Returns true if messages should be extracted and used
		/// </summary>
		internal bool ReceivePacket(double now, byte[] buffer, int bytesReceived)
		{
			byte serial = buffer[0];
			byte ackSerial = buffer[1];

			uint ackBitMask =
				(uint)buffer[2] |
				(uint)(buffer[3] << 8) |
				(uint)(buffer[4] << 16) |
				(uint)(buffer[5] << 24);

			m_owner.LogVerbose("Received packet #" + serial + " (" + bytesReceived + " bytes)");
			m_numUnackedPackets++;

			// force explicit ack sometime in the future
			m_nextKeepAlive = now + m_owner.m_configuration.m_maxExplicitAckDelay;

			HandleReceivedAck(now, ackSerial, ackBitMask);

			int diff = (serial < m_receiveWindowBase ? (serial + NetPeer.NUM_SERIALS) - m_receiveWindowBase : serial - m_receiveWindowBase);

			if (diff > NetPeer.WINDOW_SIZE)
			{
				// Not in window - reject packet
				m_owner.LogWarning("Received out-of-window packet (" + serial + ", expecting " + m_receiveWindowBase + "), rejecting!");
				return false;
			}

			if (diff == 0)
			{
				// Received expected, how nice
				//Release(pk);
				m_receiveWindowBase = (m_receiveWindowBase + 1) % NetPeer.NUM_SERIALS;

				// Release any pending early arrivals
				while ((EarlyArrivalBitMask & 1) == 1)
				{
					// found one
					//int eslot = ReceiveBase % WINDOW_SIZE; // (no +1 since receivebase has already been incremented)
					//Packet earlyPacket = EarlyArrivals[eslot];
					//EarlyArrivals[eslot] = null;
					//Release(earlyPacket);

					m_receiveWindowBase = (m_receiveWindowBase + 1) % NetPeer.NUM_SERIALS;
					EarlyArrivalBitMask = (EarlyArrivalBitMask >> 1);
				}

				EarlyArrivalBitMask = (EarlyArrivalBitMask >> 1);
				return true;
			}

			// Console.WriteLine(Name + " got early arrival packet (" + pk.Serial + ", expected " + ReceiveBase + ")");
			m_owner.LogVerbose("Got early arrival packet (" + serial + ", expected " + m_receiveWindowBase + ")");

			// Early arrival within window
			int wslot = diff;
			// EarlyArrivals[(ReceiveBase + wslot) % WINDOW_SIZE] = pk;
			EarlyArrivalBitMask |= (uint)(1 << (wslot - 1));

			return true;
		}

		private void HandleReceivedAck(double now, int ackSerial, uint ackMask)
		{
			if (ackMask != 0)
			{
				StringBuilder b = new StringBuilder();
				b.Append("Packet loss detected; serial(s) " + ((ackSerial + 1) % NetPeer.NUM_SERIALS));

				// get last SET bit in array (last received (early) packet)
				uint tmp;
				int lastIndex = 31;
				for (int i = 31; i >= 0; i--)
				{
					tmp = ackMask >> i;
					if ((tmp & 1) == 1)
					{
						lastIndex = i;
						break;
					}
				}

				// determine which packet(s) were lost
				tmp = ackMask;
				for (int i = 0; i < lastIndex; i++)
				{
					if ((tmp & 1) == 0)
						b.Append(", " + (ackSerial + (i + 2)));
					tmp = tmp >> 1;
				}

				//
				// TODO: resend the lost packets (if stored/stored part)?
				//

				throw new NotImplementedException("resend lost packets (or reliable parts)");

				m_owner.LogDebug(b.ToString());
			}

			// late ack?
			int diff = (ackSerial < m_sendWindowBase ? (ackSerial + NetPeer.NUM_SERIALS) - m_sendWindowBase : ackSerial - m_sendWindowBase);

			if (diff == NetPeer.NUM_SERIALS - 1)
			{
				// yet another ack for the previous packet, s'ok
				// Console.WriteLine(Name + " got yet another ack for " + pk.AckSerial);
				return;
			}

			if (diff > NetPeer.WINDOW_SIZE)
			{
				// ack is too early!
				m_owner.LogWarning("Found too early ack, not within window! ackSerial is " + ackSerial + " SendWindowBase is " + m_sendWindowBase);
				return;
			}

			// advance window
			int inc = diff + 1;
			m_sendWindowBase = (m_sendWindowBase + inc) % NetPeer.NUM_SERIALS;

			double wasSent = m_sendTimes[ackSerial % NetPeer.WINDOW_SIZE];
			UpdateLastSendRespondedTo(wasSent);

			m_owner.LogVerbose("Received ack for " + ackSerial + " (after " + (int)((now - wasSent) * 1000) + " ms; advancing SendWindowBase to " + m_sendWindowBase);
		}
	}
}
