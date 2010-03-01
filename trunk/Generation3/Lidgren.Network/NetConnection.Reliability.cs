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
using System.Threading;

namespace Lidgren.Network
{
	public partial class NetConnection
	{
		private ushort[] m_nextSendSequenceNumber;
		private ushort[] m_lastReceivedSequenced;

		internal List<NetOutgoingMessage>[] m_storedMessages; // naïve! replace by something better?
		internal NetBitVector m_storedMessagesNotEmpty;
	
		private ushort[] m_allReliableReceivedUpTo;
		private List<NetIncomingMessage>[] m_withheldMessages;
		internal Queue<int> m_acknowledgesToSend;
		internal double m_nextForceAckTime;

		private bool[][] m_reliableReceived;
		
		private void InitializeReliability()
		{
			int num = ((int)NetMessageType.UserReliableOrdered + NetConstants.kNetChannelsPerDeliveryMethod) - (int)NetMessageType.UserSequenced;
			m_nextSendSequenceNumber = new ushort[num];
			m_lastReceivedSequenced = new ushort[num];
			
			m_storedMessages = new List<NetOutgoingMessage>[NetConstants.kNumReliableChannels];
			m_storedMessagesNotEmpty = new NetBitVector(NetConstants.kNumReliableChannels);

			m_reliableReceived = new bool[NetConstants.kNumSequenceNumbers][]; // TODO: exchange for bit vector
			m_allReliableReceivedUpTo = new ushort[NetConstants.kNumReliableChannels];
			m_withheldMessages = new List<NetIncomingMessage>[NetConstants.kNetChannelsPerDeliveryMethod]; // only for ReliableOrdered
			m_acknowledgesToSend = new Queue<int>();
		}

		internal ushort GetSendSequenceNumber(NetMessageType mtp)
		{
			m_owner.VerifyNetworkThread();
			int slot = (int)mtp - (int)NetMessageType.UserSequenced;
			return m_nextSendSequenceNumber[slot]++;
		}

		internal int Relate(ushort seqNr, ushort lastReceived)
		{
			return (seqNr < lastReceived ? (seqNr + NetConstants.kNumSequenceNumbers) - lastReceived : seqNr - lastReceived);
		}

		// returns true if message should be rejected
		internal bool ReceivedSequencedMessage(NetMessageType mtp, ushort seqNr)
		{
			int slot = (int)mtp - (int)NetMessageType.UserSequenced;
	
			int diff = Relate(seqNr, m_lastReceivedSequenced[slot]);

			if (diff > (ushort.MaxValue / 2))
				return true; // reject; out of window
			m_lastReceivedSequenced[slot] = seqNr;
			return false;
		}

		// called the FIRST time a reliable message is sent
		private void StoreReliableMessage(double now, NetOutgoingMessage msg)
		{
			m_owner.VerifyNetworkThread();

			int reliableSlot = (int)msg.m_type - (int)NetMessageType.UserReliableUnordered;

			List<NetOutgoingMessage> list = m_storedMessages[reliableSlot];
			if (list == null)
			{
				list = new List<NetOutgoingMessage>();
				m_storedMessages[reliableSlot] = list;
			}
			Interlocked.Increment(ref msg.m_inQueueCount);
			list.Add(msg);

			if (list.Count == 1)
				m_storedMessagesNotEmpty.Set(reliableSlot, true);

			// schedule next resend
			int numSends = msg.m_numSends;
			float[] baseTimes = m_peerConfiguration.m_resendBaseTime;
			float[] multiplers = m_peerConfiguration.m_resendRTTMultiplier;
			msg.m_nextResendTime = now + baseTimes[numSends] + (m_averageRoundtripTime * multiplers[numSends]);
		}

		private void Resend(double now, NetOutgoingMessage msg)
		{
			m_owner.VerifyNetworkThread();

			int numSends = msg.m_numSends;
			float[] baseTimes = m_peerConfiguration.m_resendBaseTime;
			if (numSends >= baseTimes.Length)
			{
				// no more resends! We failed!
				int reliableSlot = (int)msg.m_type - (int)NetMessageType.UserReliableUnordered;
				List<NetOutgoingMessage> list = m_storedMessages[reliableSlot];
				list.Remove(msg);
				m_owner.LogWarning("Failed to deliver reliable message " + msg);
				return; // no more resends!
			}

			m_owner.LogVerbose("Resending " + msg);

			Interlocked.Increment(ref msg.m_inQueueCount);
			m_unsentMessages.EnqueueFirst(msg);

			msg.m_lastSentTime = now;

			// schedule next resend
			float[] multiplers = m_peerConfiguration.m_resendRTTMultiplier;
			msg.m_nextResendTime = now + baseTimes[numSends] + (m_averageRoundtripTime * multiplers[numSends]);
		}

		private void HandleIncomingAcks(int ptr, int payloadLength)
		{
			m_owner.VerifyNetworkThread();

			int numAcks = payloadLength / 3;
			if (numAcks * 3 != payloadLength)
				m_owner.LogWarning("Malformed ack message; payload length is " + payloadLength);

			byte[] buffer = m_owner.m_receiveBuffer;
			for (int i = 0; i < numAcks; i++)
			{
				ushort seqNr = (ushort)(buffer[ptr++] | (buffer[ptr++] << 8));
				NetMessageType tp = (NetMessageType)buffer[ptr++];
				// m_owner.LogDebug("Got ack for " + tp + " " + seqNr);

				// remove stored message
				int reliableSlot = (int)tp - (int)NetMessageType.UserReliableUnordered;

				List<NetOutgoingMessage> list = m_storedMessages[reliableSlot];
				if (list == null)
					continue;

				// find message
				for(int a=0;a<list.Count;a++)
				{
					NetOutgoingMessage om = list[a];
					if (om.m_sequenceNumber == seqNr)
					{
						// found!
						list.RemoveAt(a);
						Interlocked.Decrement(ref om.m_inQueueCount);

						NetException.Assert(om.m_lastSentTime != 0);

						m_lastSendRespondedTo = om.m_lastSentTime;

						if (om.m_inQueueCount < 1)
							m_owner.Recycle(om);

						break;
					}
				}

				// TODO: receipt handling

				// TODO: recycle if queuecount is < 1

			}
		}

		private void PostAcceptReliableMessage(NetMessageType mtp, ushort sequenceNumber, ushort arut)
		{
			int reliableSlot = (int)mtp - (int)NetMessageType.UserReliableUnordered;

			// step forward until next AllReliableReceivedUpTo (arut)
			bool nextArutAlreadyReceived = false;
			do
			{
				if (m_reliableReceived[reliableSlot] == null)
					m_reliableReceived[reliableSlot] = new bool[NetConstants.kNumSequenceNumbers];
				m_reliableReceived[reliableSlot][arut] = false;
				arut++; // will wrap automatically

				nextArutAlreadyReceived = m_reliableReceived[reliableSlot][arut];
				if (nextArutAlreadyReceived)
				{
					// ordered?
					if (mtp >= NetMessageType.UserReliableOrdered)
					{
						// this should be a withheld message
						int wmlidx = (int)mtp - (int)NetMessageType.UserReliableOrdered;
						bool foundWithheld = false;

						foreach (NetIncomingMessage wm in m_withheldMessages[wmlidx])
						{
							int wmSeqChan = wm.SequenceChannel;

							if (wmlidx == wmSeqChan && wm.m_sequenceNumber == arut)
							{
								// Found withheld message due for delivery
								m_owner.LogVerbose("Releasing withheld message " + wm);
								
								// AcceptMessage
								m_owner.ReleaseMessage(wm);

								foundWithheld = true;
								m_withheldMessages[wmlidx].Remove(wm);
								break;
							}
						}
						if (!foundWithheld)
							throw new NetException("Failed to find withheld message!");
					}
				}
			} while (nextArutAlreadyReceived);

			m_allReliableReceivedUpTo[reliableSlot] = arut;
		}
	}
}
