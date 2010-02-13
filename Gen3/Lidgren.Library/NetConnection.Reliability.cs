using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;

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

		internal WindowSlot[] m_windowSlots;
		internal int m_numUnackedPackets;

		internal List<NetOutgoingMessage>[] m_storedMessages;

		internal INetBitVector EarlyArrivalBitMask;

		private void ResetSlidingWindow()
		{
			m_receiveWindowBase = 0;
			m_sendWindowBase = 0;
			m_sendNext = 0;
			m_numUnackedPackets = 0;

			EarlyArrivalBitMask = new NetBitVector64();

			int wz = m_owner.m_configuration.m_windowSize;
			m_storedMessages = new List<NetOutgoingMessage>[wz];
			m_windowSlots = new WindowSlot[wz];
			for (int i = 0; i < m_windowSlots.Length; i++)
				m_windowSlots[i] = new WindowSlot();
		}

		internal bool CanSend()
		{
			if ((m_sendWindowBase + m_owner.m_configuration.m_windowSize) % NetPeer.NUM_SERIALS == m_sendNext)
				return false; // window full
			return true;
		}

		/// <summary>
		/// Returns slot to store reliable messages in
		/// </summary>
		internal WindowSlot PrepareSend(double now, out int headerSize)
		{
			byte[] buffer = m_owner.m_sendBuffer;

			WindowSlot slot = m_windowSlots[m_sendNext % m_owner.m_configuration.m_windowSize];
			slot.SentTime = now;

			m_owner.LogVerbose("Storing packet " + m_sendNext + " in store " + (m_sendNext % m_owner.m_configuration.m_windowSize));

			buffer[0] = (byte)m_sendNext;
			m_sendNext = (m_sendNext + 1) % NetPeer.NUM_SERIALS;
			m_numUnackedPackets = 0;

			buffer[1] = (byte)((m_receiveWindowBase - 1 + NetPeer.NUM_SERIALS) % NetPeer.NUM_SERIALS);

			// WriteVariableUInt32
			int ptr = 2;
			uint num1 = EarlyArrivalBitMask.First32Bits;
			while (num1 >= 0x80)
			{
				buffer[ptr++] = (byte)(num1 | 0x80);
				num1 = num1 >> 7;
			}
			buffer[ptr++] = (byte)num1;

			headerSize = ptr;

			return slot;
		}

		/// <summary>
		/// Returns true if messages should be extracted and used
		/// </summary>
		internal bool ReceivePacket(double now, byte[] buffer, int bytesReceived, out int headerSize)
		{
			byte serial = buffer[0];
			byte ackSerial = buffer[1];

			// ReadVariableUInt32
			int ptr = 2;
			int num1 = 0;
			int num2 = 0;
			while (true)
			{
				byte num3 = buffer[ptr++];
				num1 |= (num3 & 0x7f) << num2;
				num2 += 7;
				if ((num3 & 0x80) == 0)
					break;
			}

			uint ackBitMask = (uint)num1;
			headerSize = ptr;

			m_owner.LogVerbose("Received packet #" + serial + " (" + bytesReceived + " bytes)");
			m_numUnackedPackets++;

			// force explicit ack sometime in the future
			m_nextKeepAlive = now + m_owner.m_configuration.m_maxExplicitAckDelay;

			HandleReceivedAck(now, ackSerial, ackBitMask);

			int diff = (serial < m_receiveWindowBase ? (serial + NetPeer.NUM_SERIALS) - m_receiveWindowBase : serial - m_receiveWindowBase);

			if (diff > m_owner.m_configuration.m_windowSize)
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
				while (EarlyArrivalBitMask.IsSet(0))
				{
					// found one
					//int eslot = ReceiveBase % WINDOW_SIZE; // (no +1 since receivebase has already been incremented)
					//Packet earlyPacket = EarlyArrivals[eslot];
					//EarlyArrivals[eslot] = null;
					//Release(earlyPacket);

					m_receiveWindowBase = (m_receiveWindowBase + 1) % NetPeer.NUM_SERIALS;
					EarlyArrivalBitMask.ShiftRight(1);
				}

				EarlyArrivalBitMask.ShiftRight(1);
				return true;
			}

			// Console.WriteLine(Name + " got early arrival packet (" + pk.Serial + ", expected " + ReceiveBase + ")");
			m_owner.LogVerbose("Got early arrival packet (" + serial + ", expected " + m_receiveWindowBase + ")");

			// Early arrival within window
			int wslot = diff;
			EarlyArrivalBitMask.Set(wslot - 1);

			return true;
		}

		private void HandleReceivedAck(double now, int ackSerial, uint ackMask)
		{
			m_owner.VerifyNetworkThread();

			int windowSize = m_owner.m_configuration.m_windowSize;

			if (ackMask != 0)
			{
				int lost = ((ackSerial + 1) % NetPeer.NUM_SERIALS);

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

				ResendSerial(now, lost, windowSize);

				// determine which packet(s) were lost
				tmp = ackMask;
				for (int i = 0; i < lastIndex; i++)
				{
					if ((tmp & 1) == 0)
					{
						int alsoLost = (ackSerial + (i + 2));
						// b.Append(", " + (ackSerial + (i + 2)));
						ResendSerial(now, alsoLost, windowSize);
					}
					tmp = tmp >> 1;
				}
			
			}

			int diff = (ackSerial < m_sendWindowBase ? (ackSerial + NetPeer.NUM_SERIALS) - m_sendWindowBase : ackSerial - m_sendWindowBase);

			if (diff == NetPeer.NUM_SERIALS - 1)
			{
				// yet another ack for the previous packet, s'ok
				// Console.WriteLine(Name + " got yet another ack for " + pk.AckSerial);
				return;
			}

			if (diff > windowSize)
			{
				// ack is too early!
				m_owner.LogWarning("Found too early ack, not within window! ackSerial is " + ackSerial + " SendWindowBase is " + m_sendWindowBase);
				return;
			}

			// advance window
			int inc = diff + 1;

			m_sendWindowBase = (m_sendWindowBase + inc) % NetPeer.NUM_SERIALS;

			WindowSlot ackSlot = m_windowSlots[ackSerial % windowSize];
			ackSlot.NumResends = 0;
			ackSlot.StoredReliableMessages.Clear();
			UpdateLastSendRespondedTo(ackSlot.SentTime);

			if (inc > 1)
			{
				for (int i = 1; i < inc; i++)
				{
					ackSlot = m_windowSlots[(ackSerial + windowSize - i) % windowSize];
					ackSlot.NumResends = 0;
					ackSlot.StoredReliableMessages.Clear();
				}
			}

			m_owner.LogVerbose("Received ack for " + ackSerial + " (after " + (int)((now - ackSlot.SentTime) * 1000) + " ms; advancing SendWindowBase to " + m_sendWindowBase);
		}

		private void ResendSerial(double now, int lost, int windowSize)
		{
			WindowSlot lostSlot = m_windowSlots[lost % windowSize];

			m_owner.LogDebug("Resending #" + lost);

			if (lostSlot.NumResends == 0 ||
				now > lostSlot.SentTime + (m_owner.m_configuration.m_initialTimeBetweenResends * (lostSlot.NumResends + 1)))
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

					// WriteVariableUInt32
					int ptr = 2;
					uint num1 = EarlyArrivalBitMask.First32Bits;
					while (num1 >= 0x80)
					{
						buffer[ptr++] = (byte)(num1 | 0x80);
						num1 = num1 >> 7;
					}
					buffer[ptr++] = (byte)num1;

					foreach (NetOutgoingMessage om in lostSlot.StoredReliableMessages)
						ptr = om.Encode(buffer, ptr);

					m_owner.SendPacket(ptr, m_remoteEndPoint);

					lostSlot.NumResends++;
					lostSlot.SentTime = now;
				}
			}
		}
	}
}
