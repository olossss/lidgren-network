using System;
using System.Collections.Generic;

namespace Lidgren.Network2
{
	public partial class NetConnection
	{
		private const int WINDOW_SIZE = 4;

		private ushort m_nextPacketNumberToSend;

		// unacknowledged reliable messages, per packet - circular buffer
		private List<NetOutgoingMessage>[] m_packetList;
		private double[] m_packetSendTimes;
		private Queue<int> m_acksToSend;
		private int m_packetListStartNumber;
		private int m_packetListStart;
		private int m_packetListLength;

		// called on user thread
		private void ReliabilityCtor()
		{
			m_nextPacketNumberToSend = 1;
			m_packetList = new List<NetOutgoingMessage>[WINDOW_SIZE];
			for (int i = 0; i < m_packetList.Length; i++)
				m_packetList[i] = new List<NetOutgoingMessage>();
			m_acksToSend = new Queue<int>();
			m_packetSendTimes = new double[WINDOW_SIZE];
			m_packetListStart = 0;
			m_packetListStartNumber = 1;
			m_packetListLength = 0;
		}

		private bool GetSendPacket(double now, out int packetNumber, out int packetSlot)
		{
			if (m_packetListLength == WINDOW_SIZE)
			{
				// window is shut; but if the first slot does not have any reliable messages just push forward
				if (m_packetList[m_packetListStart].Count > 0)
				{
					// window is shut
					packetNumber = -1;
					packetSlot = -1;
					return false;
				}
				m_packetList[m_packetListStart].Clear();
				packetSlot = m_packetListStart;
				m_packetListStart = (m_packetListStart + 1) % WINDOW_SIZE;
				m_packetListStartNumber++;
			}
			else
			{
				packetSlot = (m_packetListStart + m_packetListLength) % WINDOW_SIZE;
				m_packetList[packetSlot].Clear();
				m_packetListLength++;
			}

			m_packetSendTimes[packetSlot] = now;
			packetNumber = m_nextPacketNumberToSend;
			m_nextPacketNumberToSend++;

			return true;
		}

		// returns roundtrip time for this packet
		private void ReceiveAcknowledge(double now, int packetNumber)
		{
			m_owner.VerifyNetworkThread(); 
			
			if (packetNumber < m_packetListStartNumber || packetNumber > (m_packetListStartNumber + m_packetListLength))
			{
				// not in list? odd
				m_owner.LogWarning("Received out-of-order acknowledge; window is " + m_packetListStartNumber + " to " + (m_packetListStartNumber + m_packetListLength) + " - ack is P#" + packetNumber);
			}
			else
			{
				int slot = (m_packetListStart + (packetNumber - m_packetListStartNumber)) % WINDOW_SIZE;
				m_owner.LogVerbose("Received ack for packet P#" + packetNumber + " - clearing " + m_packetList[packetNumber - m_packetListStartNumber].Count + " stored messages");
				m_packetList[slot].Clear();

				m_lastSendRespondedTo = m_packetSendTimes[slot];
			
				UpdateLatency(now, (float)(now - m_packetSendTimes[slot]));
			}
		}

		internal void SendAcknowledge(int packetNumber)
		{
			m_owner.VerifyNetworkThread();

			m_owner.LogVerbose("Queueing ack for R#" + packetNumber);

			lock (m_acksToSend)
				m_acksToSend.Enqueue(packetNumber);
		}
	}
}
