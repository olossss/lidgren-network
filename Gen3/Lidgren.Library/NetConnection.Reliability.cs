using System;
using System.Collections.Generic;
using System.Text;

namespace Lidgren.Network
{
	internal sealed class WindowSlot
	{
		public double SentTime;
		public List<NetOutgoingMessage> StoredReliableMessages;
		public int NumResends;

		public WindowSlot()
		{
			Reset();
		}

		public void Reset()
		{
			SentTime = 0;
			NumResends = 0;
			if (StoredReliableMessages == null)
				StoredReliableMessages = new List<NetOutgoingMessage>();
			StoredReliableMessages.Clear();
		}
	}

	public partial class NetConnection
	{
		//
		// Selective repeat sliding window
		//
		internal int m_receiveWindowBase;
		internal int m_sendWindowBase;
		internal int m_sendNext;

		internal WindowSlot[] m_windowSlots = new WindowSlot[NetPeer.WINDOW_SIZE];
		internal int m_numUnackedPackets;

		internal List<NetOutgoingMessage>[] m_storedMessages = new List<NetOutgoingMessage>[NetPeer.WINDOW_SIZE];

		internal uint EarlyArrivalBitMask;

		private void ResetSlidingWindow()
		{
			m_receiveWindowBase = 0;
			m_sendWindowBase = 0;
			m_sendNext = 0;
			m_numUnackedPackets = 0;
			for (int i = 0; i < m_windowSlots.Length; i++)
			{
				if (m_windowSlots[i] == null)
					m_windowSlots[i] = new WindowSlot();
				else
					m_windowSlots[i].Reset();
			}
		}

		internal bool CanSend()
		{
			if ((m_sendWindowBase + NetPeer.WINDOW_SIZE) % NetPeer.NUM_SERIALS == m_sendNext)
				return false; // window full
			return true;
		}

		/// <summary>
		/// Returns slot to store reliable messages in
		/// </summary>
		internal WindowSlot PrepareSend(double now)
		{
			byte[] buffer = m_owner.m_sendBuffer;

			WindowSlot slot = m_windowSlots[m_sendNext % NetPeer.WINDOW_SIZE];
			slot.SentTime = now;

			m_owner.LogVerbose("Storing packet " + m_sendNext + " in store " + (m_sendNext % NetPeer.WINDOW_SIZE));

			buffer[0] = (byte)m_sendNext;
			m_sendNext = (m_sendNext + 1) % NetPeer.NUM_SERIALS;
			m_numUnackedPackets = 0;

			buffer[1] = (byte)((m_receiveWindowBase - 1 + NetPeer.NUM_SERIALS) % NetPeer.NUM_SERIALS);
			buffer[2] = (byte)EarlyArrivalBitMask;
			buffer[3] = (byte)(EarlyArrivalBitMask >> 8);
			buffer[4] = (byte)(EarlyArrivalBitMask >> 16);
			buffer[5] = (byte)(EarlyArrivalBitMask >> 24);

			return slot;
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
			m_owner.VerifyNetworkThread();

			if (ackMask != 0)
			{
				StringBuilder b = new StringBuilder();
				int lost = ((ackSerial + 1) % NetPeer.NUM_SERIALS);
				b.Append("Packet loss detected; serial(s) " + lost);

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

				WindowSlot lostSlot = m_windowSlots[lost % NetPeer.WINDOW_SIZE];

				if (lostSlot.NumResends == 0 ||
					now > lostSlot.SentTime + (m_owner.m_configuration.m_initialTimeBetweenResends * lostSlot.NumResends))
				{

					if (lostSlot.NumResends > m_owner.m_configuration.m_maxResends)
					{
						Disconnect("Too many failed resends without ack");
					}
					else
					{
						//
						// Resend lost packets (with original packet serials!)
						//
						byte[] buffer = m_owner.m_sendBuffer;
						buffer[0] = (byte)lost;

						buffer[1] = (byte)((m_receiveWindowBase - 1 + NetPeer.NUM_SERIALS) % NetPeer.NUM_SERIALS);
						buffer[2] = (byte)EarlyArrivalBitMask;
						buffer[3] = (byte)(EarlyArrivalBitMask >> 8);
						buffer[4] = (byte)(EarlyArrivalBitMask >> 16);
						buffer[5] = (byte)(EarlyArrivalBitMask >> 24);

						int ptr = 6;

						foreach (NetOutgoingMessage om in lostSlot.StoredReliableMessages)
							ptr = om.Encode(buffer, ptr);

						m_owner.SendPacket(ptr, m_remoteEndPoint);

						// TODO: what to do with the rest of the lost packets?
						lostSlot.NumResends++;
						lostSlot.SentTime = now;
					}
					m_owner.LogDebug(b.ToString());
				}
			}

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

			WindowSlot ackSlot = m_windowSlots[ackSerial % NetPeer.WINDOW_SIZE];
			ackSlot.NumResends = 0;
			ackSlot.StoredReliableMessages.Clear();
			UpdateLastSendRespondedTo(ackSlot.SentTime);

			if (inc > 1)
			{
				for (int i = 1; i < inc; i++)
				{
					ackSlot = m_windowSlots[(ackSerial + NetPeer.WINDOW_SIZE - i) % NetPeer.WINDOW_SIZE];
					ackSlot.NumResends = 0;
					ackSlot.StoredReliableMessages.Clear();
				}
			}

			m_owner.LogVerbose("Received ack for " + ackSerial + " (after " + (int)((now - ackSlot.SentTime) * 1000) + " ms; advancing SendWindowBase to " + m_sendWindowBase);
		}

	}
}
