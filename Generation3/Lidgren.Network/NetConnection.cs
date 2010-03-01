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
using System.Diagnostics;
using System.Net;
using System.Threading;

namespace Lidgren.Network
{
	[DebuggerDisplay("RemoteEndPoint={m_remoteEndPoint} Status={m_status}")]
	public partial class NetConnection
	{
		private NetPeer m_owner;
		internal IPEndPoint m_remoteEndPoint;
		internal double m_lastHeardFrom;
		internal NetQueue<NetOutgoingMessage> m_unsentMessages;
		internal NetConnectionStatus m_status;
		private double m_lastSentUnsentMessages;
		private float m_throttleDebt;
		private NetPeerConfiguration m_peerConfiguration;
		internal NetConnectionStatistics m_statistics;

		internal PendingConnectionStatus m_pendingStatus = PendingConnectionStatus.NotPending;
		internal string m_pendingDenialReason;

		public object Tag;

		/// <summary>
		/// Statistics for this particular connection
		/// </summary>
		public NetConnectionStatistics Statistics { get { return m_statistics; } }

		internal NetConnection(NetPeer owner, IPEndPoint remoteEndPoint)
		{
			m_owner = owner;
			m_peerConfiguration = m_owner.m_configuration;
			m_remoteEndPoint = remoteEndPoint;
			m_unsentMessages = new NetQueue<NetOutgoingMessage>(16);
			m_status = NetConnectionStatus.None;

			double now = NetTime.Now;
			m_nextPing = now + 5.0f;
			m_nextKeepAlive = now + 6.0f + m_peerConfiguration.m_keepAliveDelay;
			m_lastSentUnsentMessages = now;
			m_lastSendRespondedTo = now;
			m_statistics = new NetConnectionStatistics(this);

			InitializeReliability();
		}

		// run on network thread
		internal void Heartbeat(double now)
		{
			m_owner.VerifyNetworkThread();

			if (m_connectRequested)
				SendConnect();

			// keepalive
			KeepAliveHeartbeat(now);

			// queue resends
			// TODO: only do this every x millisecond
			for (int i = 0; i < m_storedMessages.Length; i++)
			{
				if (m_storedMessagesNotEmpty.Get(i))
				{
					foreach (NetOutgoingMessage om in m_storedMessages[i])
					{
						if (now >= om.m_nextResendTime)
						{
							Resend(now, om);
							break; // need to break out here; collection may have been modified
						}
					}
				}
			}


			// send unsent messages; high priority first
			byte[] buffer = m_owner.m_sendBuffer;
			int ptr = 0;

			float throttle = m_peerConfiguration.m_throttleBytesPerSecond;
			if (throttle > 0)
			{
				double frameLength = now - m_lastSentUnsentMessages;
				if (m_throttleDebt > 0)
					m_throttleDebt -= (float)(frameLength * throttle);
				m_lastSentUnsentMessages = now;
			}

			int mtu = m_peerConfiguration.MaximumTransmissionUnit;

			float throttleThreshold = m_peerConfiguration.m_throttlePeakBytes;
			if (m_throttleDebt < throttleThreshold)
			{
				//
				// Send new unsent messages
				//
				while (m_unsentMessages.Count > 0)
				{
					if (m_throttleDebt >= throttleThreshold)
						break;

					NetOutgoingMessage msg = m_unsentMessages.TryDequeue();
					if (msg == null)
						continue;
					Interlocked.Decrement(ref msg.m_inQueueCount);

					int msgPayloadLength = msg.LengthBytes;
					msg.m_lastSentTime = now;

					if (ptr > 0 && (ptr + NetPeer.kMaxPacketHeaderSize + msgPayloadLength) > mtu)
					{
						// send packet and start new packet
						m_owner.SendPacket(ptr, m_remoteEndPoint);
						m_statistics.m_sentPackets++;
						m_statistics.m_sentBytes += ptr;
						m_throttleDebt += ptr;
						ptr = 0;
					}

					//
					// encode message
					//

					ptr = msg.Encode(buffer, ptr, this);

					if (msg.m_type >= NetMessageType.UserReliableUnordered && msg.m_numSends == 1)
					{
						// message is sent for the first time, and is reliable, store for resend
						StoreReliableMessage(now, msg);
					}

					// room to piggyback some acks?
					if (m_acknowledgesToSend.Count > 0)
					{
						int payloadLeft = (mtu - ptr) - NetPeer.kMaxPacketHeaderSize;
						if (payloadLeft > 9)
						{
							// yes, add them as a regular message
							ptr = NetOutgoingMessage.EncodeAcksMessage(m_owner.m_sendBuffer, ptr, this, (payloadLeft - 3));

							if (m_acknowledgesToSend.Count < 1)
								m_nextForceAckTime = double.MaxValue;
						}
					}

					if (msg.m_type == NetMessageType.Library && msg.m_libType == NetMessageLibraryType.Disconnect)
					{
						FinishDisconnect();
						break;
					}

					if (msg.m_inQueueCount < 1)
						m_owner.Recycle(msg);
				}

				if (ptr > 0)
				{
					m_owner.SendPacket(ptr, m_remoteEndPoint);
					m_statistics.m_sentPackets++;
					m_statistics.m_sentBytes += ptr;
					m_throttleDebt += ptr;
				}
			}
		}

		internal void HandleUserMessage(double now, NetMessageType mtp, ushort channelSequenceNumber, int ptr, int payloadLength)
		{
			m_owner.VerifyNetworkThread();

			try
			{
				if (!m_peerConfiguration.IsMessageTypeEnabled(NetIncomingMessageType.Data))
					return;

				NetDeliveryMethod ndm = NetPeer.GetDeliveryMethod(mtp);

				//
				// Unreliable
				//
				if (ndm == NetDeliveryMethod.Unreliable)
				{
					AcceptMessage(mtp, channelSequenceNumber, ptr, payloadLength);
					return;
				}

				//
				// UnreliableSequenced
				//
				if (ndm == NetDeliveryMethod.UnreliableSequenced)
				{
					bool reject = ReceivedSequencedMessage(mtp, channelSequenceNumber);
					if (!reject)
						AcceptMessage(mtp, channelSequenceNumber, ptr, payloadLength);
					return;
				}

				//
				// Reliable delivery methods below
				//

				// queue ack
				m_acknowledgesToSend.Enqueue((int)channelSequenceNumber | ((int)mtp << 16));
				if (m_nextForceAckTime == double.MaxValue)
					m_nextForceAckTime = now + m_peerConfiguration.m_maxAckDelayTime;

				if (ndm == NetDeliveryMethod.ReliableSequenced)
				{
					bool reject = ReceivedSequencedMessage(mtp, channelSequenceNumber);
					if (!reject)
						AcceptMessage(mtp, channelSequenceNumber, ptr, payloadLength);
					return;
				}

				// relate to all received up to
				int reliableSlot = (int)mtp - (int)NetMessageType.UserReliableUnordered;
				ushort arut = m_allReliableReceivedUpTo[reliableSlot];
				int diff = Relate(channelSequenceNumber, arut);

				if (diff > (ushort.MaxValue / 2))
				{
					// Reject duplicate
					//m_statistics.CountDuplicateMessage(msg);
					m_owner.LogVerbose("Rejecting duplicate reliable " + mtp + " " + channelSequenceNumber);
					return;
				}

				if (diff == 0)
				{
					// Right on time
					AcceptMessage(mtp, channelSequenceNumber, ptr, payloadLength);
					PostAcceptReliableMessage(mtp, channelSequenceNumber, arut);
					return;
				}

				//
				// Early reliable message - we must check if it's already been received
				//

				/*
				// get bools list we must check
				bool[] recList = m_reliableReceived[relChanNr];
				if (recList == null)
				{
					recList = new bool[NetConstants.NumSequenceNumbers];
					m_reliableReceived[relChanNr] = recList;
				}

			if (recList[msg.m_sequenceNumber])
			{
				// Reject duplicate
				m_statistics.CountDuplicateMessage(msg);
				m_owner.LogVerbose("Rejecting(2) duplicate reliable " + msg, this);
				return;
			}

			// It's an early reliable message
			if (m_reliableReceived[relChanNr] == null)
				m_reliableReceived[relChanNr] = new bool[NetConstants.NumSequenceNumbers];
			m_reliableReceived[relChanNr][msg.m_sequenceNumber] = true;
				*/

				//
				// It's not a duplicate; mark as received. Release if it's unordered, else withhold
				//

				if (ndm == NetDeliveryMethod.ReliableUnordered)
				{
					AcceptMessage(mtp, channelSequenceNumber, ptr, payloadLength);
					return;
				}

				//
				// Only ReliableOrdered left here; withhold it
				//

				/*

			// Early ordered message; withhold
			List<IncomingNetMessage> wmlist = m_withheldMessages[relChanNr];
			if (wmlist == null)
			{
				wmlist = new List<IncomingNetMessage>();
				m_withheldMessages[relChanNr] = wmlist;
			}

			m_owner.LogVerbose("Withholding " + msg + " (waiting for " + arut + ")", this);
			wmlist.Add(msg);
			return;

				 */

				return;
			}
			catch (Exception ex)
			{
#if DEBUG
				throw new NetException("Message generated exception: " + ex, ex);
#else
				m_owner.LogError("Message generated exception: " + ex);
				ptr += payloadLength;
				return;
#endif
			}
		}

		private void AcceptMessage(NetMessageType mtp, ushort seqNr, int ptr, int payloadLength)
		{
			// release to application
			NetIncomingMessage im = m_owner.CreateIncomingMessage(NetIncomingMessageType.Data, m_owner.m_receiveBuffer, ptr, payloadLength);
			im.m_messageType = mtp;
			im.m_sequenceNumber = seqNr;
			im.m_senderConnection = this;
			im.m_senderEndPoint = m_remoteEndPoint;

			m_owner.LogVerbose("Releasing " + im);
			m_owner.ReleaseMessage(im);
		}

		internal void HandleLibraryMessage(double now, NetMessageLibraryType libType, int ptr, int payloadLength)
		{
			m_owner.VerifyNetworkThread();

			switch (libType)
			{
				case NetMessageLibraryType.Error:
					m_owner.LogWarning("Received NetMessageLibraryType.Error message!");
					break;
				case NetMessageLibraryType.Connect:
				case NetMessageLibraryType.ConnectResponse:
				case NetMessageLibraryType.ConnectionEstablished:
				case NetMessageLibraryType.Disconnect:
					HandleIncomingHandshake(libType, ptr, payloadLength);
					break;
				case NetMessageLibraryType.KeepAlive:
					// no operation, we just want the acks
					break;
				case NetMessageLibraryType.Ping:
					if (payloadLength > 0)
						HandleIncomingPing(m_owner.m_receiveBuffer[ptr]);
					else
						m_owner.LogWarning("Received malformed ping");
					break;
				case NetMessageLibraryType.Pong:
					if (payloadLength > 0)
						HandleIncomingPong(m_owner.m_receiveBuffer[ptr]);
					else
						m_owner.LogWarning("Received malformed pong");
					break;
				case NetMessageLibraryType.Acknowledge:
					HandleIncomingAcks(ptr, payloadLength);
					break;
				default:
					throw new NotImplementedException();
			}

			return;
		}

		public void SendMessage(NetOutgoingMessage msg, NetDeliveryMethod channel)
		{
			if (msg.IsSent)
				throw new NetException("Message has already been sent!");
			msg.m_type = (NetMessageType)channel;
			EnqueueOutgoingMessage(msg);
		}

		internal void EnqueueOutgoingMessage(NetOutgoingMessage msg)
		{
			m_unsentMessages.Enqueue(msg);
			Interlocked.Increment(ref msg.m_inQueueCount);
		}

		public void Disconnect(string byeMessage)
		{
			// called on user thread
			if (m_status == NetConnectionStatus.None || m_status == NetConnectionStatus.Disconnected)
				return;

			m_owner.LogVerbose("Disconnect requested for " + this);
			m_disconnectByeMessage = byeMessage;

			// loosen up throttling
			m_throttleDebt = -m_owner.m_configuration.m_throttlePeakBytes;

			// shorten resend times
			for (int i = 0; i < m_storedMessages.Length; i++)
			{
				List<NetOutgoingMessage> list = m_storedMessages[i];
				if (list != null)
				{
					foreach (NetOutgoingMessage om in list)
						om.m_nextResendTime = (om.m_nextResendTime * 0.8) - 0.05;
				}
			}

			NetOutgoingMessage bye = m_owner.CreateLibraryMessage(NetMessageLibraryType.Disconnect, byeMessage);
			EnqueueOutgoingMessage(bye);
		}

		public void Approve()
		{
			if (!m_peerConfiguration.IsMessageTypeEnabled(NetIncomingMessageType.ConnectionApproval))
				m_owner.LogError("Approve() called but ConnectionApproval is not enabled in NetPeerConfiguration!");

			if (m_pendingStatus != PendingConnectionStatus.Pending)
			{
				m_owner.LogWarning("Approve() called on non-pending connection!");
				return;
			}
			m_pendingStatus = PendingConnectionStatus.Approved;
		}

		public void Deny(string reason)
		{
			if (!m_peerConfiguration.IsMessageTypeEnabled(NetIncomingMessageType.ConnectionApproval))
				m_owner.LogError("Deny() called but ConnectionApproval is not enabled in NetPeerConfiguration!");

			if (m_pendingStatus != PendingConnectionStatus.Pending)
			{
				m_owner.LogWarning("Deny() called on non-pending connection!");
				return;
			}
			m_pendingStatus = PendingConnectionStatus.Denied;
			m_pendingDenialReason = reason;
		}

		internal void Dispose()
		{
			m_owner = null;
			m_unsentMessages = null;
		}

		public override string ToString()
		{
			return "[NetConnection to " + m_remoteEndPoint + "]";
		}
	}
}
