using System;
using System.Collections.Generic;
using System.Text;

namespace Lidgren.Network
{
	public sealed partial class NetConnection
	{
		internal double[] m_earliestResend;
		internal Queue<int> m_acknowledgesToSend;
		internal ushort[] m_nextExpectedSequence;
		internal List<NetMessage>[] m_storedMessages;
		internal List<NetMessage> m_withheldMessages;
		internal List<NetMessage> m_removeList;
		internal uint[][] m_receivedSequences;
		private int[] m_nextSequenceToSend;
		private uint[] m_currentSequenceRound;

		internal void InitializeReliability()
		{
			m_storedMessages = new List<NetMessage>[NetConstants.NumReliableChannels];
			m_withheldMessages = new List<NetMessage>(2);
			m_nextExpectedSequence = new ushort[NetConstants.NumSequenceChannels];
			m_nextSequenceToSend = new int[NetConstants.NumSequenceChannels];

			m_currentSequenceRound = new uint[NetConstants.NumSequenceChannels];
			for (int i = 0; i < m_currentSequenceRound.Length; i++)
				m_currentSequenceRound[i] = NetConstants.NumKeptSequenceNumbers;

			m_receivedSequences = new uint[NetConstants.NumSequenceChannels][];
			for (int i = 0; i < m_receivedSequences.Length; i++)
				m_receivedSequences[i] = new uint[NetConstants.NumKeptSequenceNumbers];

			m_earliestResend = new double[NetConstants.NumReliableChannels];
			for (int i = 0; i < m_earliestResend.Length; i++)
				m_earliestResend[i] = double.MaxValue;

			m_acknowledgesToSend = new Queue<int>(4);
			m_removeList = new List<NetMessage>(4);
		}

		internal void ResetReliability()
		{
			for (int i = 0; i < m_storedMessages.Length; i++)
				m_storedMessages[i] = null;
			m_withheldMessages.Clear();

			for(int i=0;i<NetConstants.NumSequenceChannels;i++)
			{
				m_nextExpectedSequence[i] = 0;
				m_nextSequenceToSend[i] = 0;
				m_currentSequenceRound[i] = NetConstants.NumKeptSequenceNumbers;
			}

			for (int i = 0; i < m_receivedSequences.Length; i++)
			{
				for (int o = 0; o < m_receivedSequences[i].Length; o++)
					m_receivedSequences[i][o] = 0;
			}

			m_acknowledgesToSend.Clear();
			m_removeList.Clear();
		}

		/// <summary>
		/// Returns positive numbers for early, 0 for as expected, negative numbers for late message
		/// Does NOT increase expected sequence number, but does set round
		/// </summary>
		private int RelateToExpected(int receivedSequenceNumber, int chanIdx, out bool isDuplicate)
		{
			int bufIdx = receivedSequenceNumber % NetConstants.NumKeptSequenceNumbers;

			int expected = m_nextExpectedSequence[chanIdx];
			uint round = m_currentSequenceRound[chanIdx];

			int diff = expected - receivedSequenceNumber;
			if (diff < -NetConstants.EarlyArrivalWindowSize)
				diff += NetConstants.NumSequenceNumbers;
			else if (diff > NetConstants.EarlyArrivalWindowSize)
				diff -= NetConstants.NumSequenceNumbers;

			if (round - m_receivedSequences[chanIdx][bufIdx] < NetConstants.NumKeptSequenceNumbers / 3)
			{
				isDuplicate = true;
			}
			else
			{
				isDuplicate = false;
				m_receivedSequences[chanIdx][bufIdx] = round;

				// advance rounds
				m_currentSequenceRound[chanIdx]++;
			}

			return -diff;
		}

		private void SetSequenceNumber(NetMessage msg)
		{
			int idx = (int)msg.m_sequenceChannel;
			int nr = m_nextSequenceToSend[idx];
			msg.m_sequenceNumber = nr;
			nr++;
			if (nr >= NetConstants.NumSequenceNumbers)
				nr = 0;
			m_nextSequenceToSend[idx] = nr;
		}

		internal void StoreMessage(double now, NetMessage msg)
		{
			int chanBufIdx = (int)msg.m_sequenceChannel - (int)NetChannel.ReliableUnordered;

			List<NetMessage> list = m_storedMessages[chanBufIdx];
			if (list == null)
			{
				list = new List<NetMessage>();
				m_storedMessages[chanBufIdx] = list;
			}
			list.Add(msg);

			m_owner.LogVerbose("Stored " + msg, this);

			// schedule resend
			float multiplier = (1 + (msg.m_numSent * msg.m_numSent)) * m_owner.m_config.m_resendTimeMultiplier;
			double nextResend = now + (0.025f + (float)m_currentAvgRoundtrip * 1.1f * multiplier);
			msg.m_nextResend = nextResend;

			// earliest?
			if (nextResend < m_earliestResend[chanBufIdx])
				m_earliestResend[chanBufIdx] = nextResend;
		}

		internal void ResendMessages(double now)
		{
			for (int i = 0; i < m_storedMessages.Length; i++)
			{
				List<NetMessage> list = m_storedMessages[i];
				if (list == null || list.Count < 1)
					continue;

				if (now > m_earliestResend[i])
				{
					double newEarliest = double.MaxValue;
					foreach (NetMessage msg in list)
					{
						double resend = msg.m_nextResend;
						if (now > resend)
						{
							// Re-enqueue message in unsent list
							m_owner.LogVerbose("Resending " + msg, this);
							m_statistics.CountMessageResent(msg.m_type);
							m_removeList.Add(msg);
							m_unsentMessages.Enqueue(msg);
						}
						if (resend < newEarliest)
							newEarliest = resend;
					}

					m_earliestResend[i] = newEarliest;
					foreach (NetMessage msg in m_removeList)
						list.Remove(msg);
					m_removeList.Clear();
				}
			}
		}

		/// <summary>
		/// Create ack message(s) for sending
		/// </summary>
		private void CreateAckMessages()
		{
			int mtuBits = ((m_owner.m_config.m_maximumTransmissionUnit - 10) / 3) * 8;

			NetMessage ackMsg = null;
			int numAcks = m_acknowledgesToSend.Count;
			for (int i = 0; i < numAcks; i++)
			{
				if (ackMsg == null)
				{
					ackMsg = m_owner.CreateMessage();
					ackMsg.m_sequenceChannel = NetChannel.Unreliable;
					ackMsg.m_type = NetMessageLibraryType.Acknowledge;
				}

				int ack = m_acknowledgesToSend.Dequeue();

				ackMsg.m_data.Write((byte)((ack >> 16) & 255));
				ackMsg.m_data.Write((byte)(ack & 255));
				ackMsg.m_data.Write((byte)((ack >> 8) & 255));

				//NetChannel ac = (NetChannel)(ack >> 16);
				//int asn = ack & ushort.MaxValue;
				//LogVerbose("Sending ack " + ac + "|" + asn);

				if (ackMsg.m_data.LengthBits >= mtuBits && m_acknowledgesToSend.Count > 0)
				{
					// send and begin again
					m_unsentMessages.Enqueue(ackMsg);
					ackMsg = null;
				}
			}

			if (ackMsg != null)
				m_unsentMessages.EnqueueFirst(ackMsg); // push acks to front of queue

			m_statistics.CountAcknowledgesSent(numAcks);
		}

		internal void HandleAckMessage(NetMessage ackMessage)
		{
			int len = ackMessage.m_data.LengthBytes;
			if ((len % 3) != 0)
			{
				if ((m_owner.m_enabledMessageTypes & NetMessageType.BadMessageReceived) == NetMessageType.BadMessageReceived)
					m_owner.NotifyApplication(NetMessageType.BadMessageReceived, "Malformed ack message; length must be multiple of 3; it's " + len, this);
				return;
			}

			for (int i = 0; i < len; i += 3)
			{

				NetChannel chan = (NetChannel)ackMessage.m_data.ReadByte();
				int seqNr = ackMessage.m_data.ReadUInt16();

				// LogWrite("Acknowledgement received: " + chan + "|" + seqNr);
				m_statistics.CountAcknowledgesReceived(1);

				// remove saved message
				int chanIdx = (int)chan - (int)NetChannel.ReliableUnordered;
				if (chanIdx < 0)
				{
					if ((m_owner.m_enabledMessageTypes & NetMessageType.BadMessageReceived) == NetMessageType.BadMessageReceived)
						m_owner.NotifyApplication(NetMessageType.BadMessageReceived, "Malformed ack message; indicated netchannel " + chan, this);
					continue;
				}

				List<NetMessage> list = m_storedMessages[chanIdx];
				if (list != null)
				{
					int cnt = list.Count;
					if (cnt > 0)
					{
						for (int o = 0; o < cnt; o++)
						{
							NetMessage msg = list[o];
							if (msg.m_sequenceNumber == seqNr)
							{
								m_owner.LogVerbose("Got ack, removed from storage: " + msg, this);

								//LogWrite("Removed stored message: " + msg);
								list.RemoveAt(o);

								// reduce estimated amount of packets on wire
								//CongestionCountAck(msg.m_packetNumber);

								// fire receipt
								if (msg.m_receiptData != null)
									m_owner.FireReceipt(this, msg.m_receiptData);

								// recycle
								msg.m_data.m_refCount--;
								if (msg.m_data.m_refCount <= 0)
									m_owner.m_bufferPool.Push(msg.m_data); // time to recycle buffer
	
								msg.m_data = null;
								m_owner.m_messagePool.Push(msg);

								break;
							}
						}
					}
				}
			}

			// recycle
			NetBuffer rb = ackMessage.m_data;
			rb.m_refCount = 0; // ack messages can't be used by more than one message
			ackMessage.m_data = null;

			m_owner.m_bufferPool.Push(rb);
			m_owner.m_messagePool.Push(ackMessage);
		}
	}
}
