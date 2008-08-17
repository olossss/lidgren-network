using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Diagnostics;

namespace Lidgren.Network
{
	/// <summary>
	/// Represents a connection between this host and a remote endpoint
	/// </summary>
	[DebuggerDisplay("RemoteEndpoint = {m_remoteEndPoint}")]
	public partial class NetConnection
	{
		private NetBase m_owner;
		internal IPEndPoint m_remoteEndPoint;
		internal NetQueue<NetMessage> m_unsentMessages;
		internal NetQueue<NetMessage> m_lockedUnsentMessages;
		internal NetConnectionStatus m_status;
		private byte[] m_hailData;
		private double m_ackWithholdingStarted;
		private float m_throttleDebt;
		private double m_lastSentUnsentMessages;

		private double m_futureClose;
		private string m_futureDisconnectReason;

		private bool m_isInitiator; // if true: we sent Connect; if false: we received Connect
		private double m_handshakeInitiated;
		private int m_handshakeAttempts;

		/// <summary>
		/// Remote endpoint for this connection
		/// </summary>
		public IPEndPoint RemoteEndpoint { get { return m_remoteEndPoint; } }

		/// <summary>
		/// For application use
		/// </summary>
		public object Tag;

		/// <summary>
		/// Number of message which has not yet been sent
		/// </summary>
		public int UnsentMessagesCount { get { return m_unsentMessages.Count; } }

		/// <summary>
		/// Gets the status of the connection
		/// </summary>
		public NetConnectionStatus Status { get { return m_status; } }

		internal void SetStatus(NetConnectionStatus status, string reason)
		{
			if (m_status == status)
				return;

			//m_owner.LogWrite("New connection status: " + status + " (" + reason + ")");
			NetConnectionStatus oldStatus = m_status;
			m_status = status;

			m_owner.NotifyStatusChange(this, reason);
		}

		internal NetConnection(NetBase owner, IPEndPoint remoteEndPoint, byte[] hailData)
		{
			m_owner = owner;
			m_remoteEndPoint = remoteEndPoint;
			m_hailData = hailData;
			m_futureClose = double.MaxValue;

			m_throttleDebt = owner.m_config.m_throttleBytesPerSecond; // slower start

			m_statistics = new NetConnectionStatistics(this, 1.0f);
			m_unsentMessages = new NetQueue<NetMessage>(6);
			m_lockedUnsentMessages = new NetQueue<NetMessage>(3);

			InitializeReliability();
			InitializeFragmentation();
			//InitializeCongestionControl(32);
		}

		/// <summary>
		/// Queue message for sending
		/// </summary>
		public void SendMessage(NetBuffer data, NetChannel channel)
		{
			SendMessage(data, channel, null, false);
		}

		/// <summary>
		/// Queue a reliable message for sending. When it has arrived ReceiptReceived will be fired on owning NetBase, and the ReceiptEventArgs will contain the object passed to this method.
		/// </summary>
		public void SendMessage(NetBuffer data, NetChannel channel, NetBuffer receiptData)
		{
			SendMessage(data, channel, receiptData, false);
		}

		// TODO: Use this with TRUE isLibraryThread for internal sendings (acks etc)

		internal void SendMessage(NetBuffer data, NetChannel channel, NetBuffer receiptData, bool isLibraryThread)
		{
			if (m_status != NetConnectionStatus.Connected)
				throw new NetException("Status must be Connected to send messages");

			if (data.LengthBytes > m_owner.m_config.m_maximumTransmissionUnit)
			{
				//
				// Fragmented message
				//

				int dataLen = data.LengthBytes;
				int chunkSize = m_owner.m_config.m_maximumTransmissionUnit - 10; // header
				int numFragments = dataLen / chunkSize;
				if (chunkSize * numFragments < dataLen)
					numFragments++;

				ushort fragId = m_nextSendFragmentId++;

				for (int i = 0; i < numFragments; i++)
				{
					NetMessage fmsg = m_owner.m_messagePool.Pop();
					fmsg.m_type = NetMessageLibraryType.UserFragmented;

					NetBuffer fragBuf = m_owner.CreateBuffer();
					fragBuf.Write(fragId);
					fragBuf.WriteVariableUInt32((uint)i);
					fragBuf.WriteVariableUInt32((uint)numFragments);

					if (i < numFragments - 1)
					{
						// normal fragment
						fragBuf.Write(data.Data, i * chunkSize, chunkSize);
					}
					else
					{
						// last fragment
						int bitsInLast = data.LengthBits - (chunkSize * (numFragments - 1) * 8);
						int bytesInLast = dataLen - (chunkSize * (numFragments - 1));
						fragBuf.Write(data.Data, i * chunkSize, bytesInLast);
					}
					fmsg.m_data = fragBuf;
					fmsg.m_data.m_refCount++;

					fmsg.m_numSent = 0;
					fmsg.m_nextResend = double.MaxValue;
					fmsg.m_sequenceChannel = channel;
					fmsg.m_sequenceNumber = -1;
					fmsg.m_receiptData = receiptData;

					if (isLibraryThread)
					{
						m_unsentMessages.Enqueue(fmsg);
					}
					else
					{
						lock (m_lockedUnsentMessages)
							m_lockedUnsentMessages.Enqueue(fmsg);
					}
				}

				return;
			}

			//
			// Normal, unfragmented, message
			//

			NetMessage msg = m_owner.m_messagePool.Pop();
			msg.m_type = NetMessageLibraryType.User;
			msg.m_data = data;
			msg.m_data.m_refCount++;
			msg.m_numSent = 0;
			msg.m_nextResend = double.MaxValue;
			msg.m_sequenceChannel = channel;
			msg.m_sequenceNumber = -1;
			msg.m_receiptData = receiptData;

			if (isLibraryThread)
			{
				m_unsentMessages.Enqueue(msg);
			}
			else
			{
				lock (m_lockedUnsentMessages)
					m_lockedUnsentMessages.Enqueue(msg);
			}
		}

		internal void Connect()
		{
			m_isInitiator = true;
			m_handshakeInitiated = NetTime.Now;
			m_futureClose = double.MaxValue;
			m_futureDisconnectReason = null;

			NetMessage msg = m_owner.CreateSystemMessage(NetSystemType.Connect);
			msg.m_data.Write(m_owner.Configuration.ApplicationIdentifier);
			if (m_hailData != null && m_hailData.Length > 0)
				msg.m_data.Write(m_hailData);
			m_unsentMessages.Enqueue(msg);
			SetStatus(NetConnectionStatus.Connecting, "Connecting");
		}

		internal void Heartbeat(double now)
		{
			if (m_status == NetConnectionStatus.Disconnected)
				return;

			//CongestionHeartbeat(now);

			// drain messages from application into main unsent list
			lock (m_lockedUnsentMessages)
			{
				NetMessage lm;
				while ((lm = m_lockedUnsentMessages.Dequeue()) != null)
					m_unsentMessages.Enqueue(lm);
			}

			if (m_status == NetConnectionStatus.Connecting)
			{
				if (now - m_handshakeInitiated > m_owner.Configuration.HandshakeAttemptRepeatDelay)
				{
					if (m_handshakeAttempts >= m_owner.Configuration.HandshakeAttemptsMaxCount)
					{
						Disconnect("No answer from remote host", 0, false);
						return;
					}
					m_handshakeAttempts++;
					if (m_isInitiator)
					{
						m_owner.LogWrite("Re-sending Connect");
						Connect();
					}
					else
					{
						m_owner.LogWrite("Re-sending ConnectResponse");
						m_handshakeInitiated = now;

						m_unsentMessages.Enqueue(m_owner.CreateSystemMessage(NetSystemType.ConnectResponse));
					}
				}
			}
			else if (m_status == NetConnectionStatus.Connected)
			{
				// send ping?
				CheckPing(now);
			}

			if (now > m_futureClose)
				FinalizeDisconnect();

			// Resend all packets that has reached a mature age
			ResendMessages(now);

			// send all unsent messages
			SendUnsentMessages(now);
		}

		private void FinalizeDisconnect()
		{
			SetStatus(NetConnectionStatus.Disconnected, m_futureDisconnectReason);
			ResetReliability();
		}

		private void SendUnsentMessages(double now)
		{
			// Add any acknowledges to unsent messages
			if (m_acknowledgesToSend.Count > 0)
			{
				if (m_unsentMessages.Count < 1)
				{
					// Wait before sending acknowledges?
					if (m_ackMaxDelayTime > 0.0f)
					{
						if (m_ackWithholdingStarted == 0.0)
						{
							m_ackWithholdingStarted = now;
						}
						else
						{
							if (now - m_ackWithholdingStarted < m_ackMaxDelayTime)
								return; // don't send (only) acks just yet
							// send acks "explicitly" ie. without any other message being sent
							m_ackWithholdingStarted = 0.0;
						}
					}
				}

				// create ack messages and add to m_unsentMessages
				CreateAckMessages();
			}

			if (m_unsentMessages.Count < 1)
				return;

			// throttling
			float throttle = m_owner.m_config.ThrottleBytesPerSecond;
			float maxSendBytes = float.MaxValue;
			if (throttle > 0)
			{
				double frameLength = now - m_lastSentUnsentMessages;

				//int wasDebt = (int)m_throttleDebt;
				if (m_throttleDebt > 0)
					m_throttleDebt -= (float)(frameLength * (double)m_owner.m_config.ThrottleBytesPerSecond);
				//int nowDebt = (int)m_throttleDebt;
				//if (nowDebt != wasDebt)
				//	LogWrite("THROTTLE worked off -" + (nowDebt - wasDebt) + " bytes = " + m_throttleDebt);

				m_lastSentUnsentMessages = now;

				maxSendBytes = throttle - m_throttleDebt;
				if (maxSendBytes < 0)
					return; // throttling; no bytes allowed to be sent
			}

			int mtu = m_owner.Configuration.MaximumTransmissionUnit;
			int messagesInPacket = 0;
			NetBuffer sendBuffer = m_owner.m_sendBuffer;
			sendBuffer.Reset();
			while (m_unsentMessages.Count > 0)
			{
				NetMessage msg = m_unsentMessages.Peek();
				int estimatedMessageSize = msg.m_data.LengthBytes + 5;

				// check if this message fits the throttle window
				if (estimatedMessageSize > maxSendBytes) // TODO: Allow at last one message if no debt
					break;

				// need to send packet and start a new one?
				if (messagesInPacket > 0 && sendBuffer.LengthBytes + estimatedMessageSize > mtu)
				{
					m_owner.SendPacket(m_remoteEndPoint);
					int sendLen = sendBuffer.LengthBytes;
					m_statistics.CountPacketSent(sendLen);
					//LogWrite("THROTTLE Send packet +" + sendLen + " bytes = " + m_throttleDebt + " (maxSendBytes " + maxSendBytes + " estimated " + estimatedMessageSize + ")");
					m_throttleDebt += sendLen;
					sendBuffer.Reset();
				}

				if (msg.m_sequenceNumber == -1)
					SetSequenceNumber(msg);

				// pop and encode message
				m_unsentMessages.Dequeue();
				int pre = sendBuffer.m_bitLength;
				msg.m_data.m_readPosition = 0;
				msg.Encode(sendBuffer);

				int encLen = (sendBuffer.m_bitLength - pre) / 8;
				m_statistics.CountMessageSent(msg, encLen);
				maxSendBytes -= encLen;

				if (msg.m_sequenceChannel >= NetChannel.ReliableUnordered)
				{
					// reliable; store message (incl. buffer)
					msg.m_numSent++;
					StoreMessage(now, msg);
				}
				else
				{
					// not reliable, don't store - recycle at once
					m_owner.m_bufferPool.Push(msg.m_data);
					msg.m_data = null;
					m_owner.m_messagePool.Push(msg);
				}
				messagesInPacket++;
			}

			// send current packet
			if (messagesInPacket > 0)
			{
				m_owner.SendPacket(m_remoteEndPoint);
				int sendLen = sendBuffer.LengthBytes;
				m_statistics.CountPacketSent(sendLen);
				//LogWrite("THROTTLE Send packet +" + sendLen + " bytes = " + m_throttleDebt);
				m_throttleDebt += sendLen;

			}
		}

		internal void HandleUserMessage(NetMessage msg)
		{
			int seqNr = msg.m_sequenceNumber;
			int chanNr = (int)msg.m_sequenceChannel;
			bool isDuplicate = false;

			int relation = RelateToExpected(seqNr, chanNr, out isDuplicate);

			//
			// Unreliable
			//
			if (msg.m_sequenceChannel == NetChannel.Unreliable)
			{
				// It's all good; add message
				if (isDuplicate)
				{
					m_statistics.CountDuplicateMessage(msg);
					m_owner.LogVerbose("Rejecting duplicate " + msg);
				}
				else
				{
					AcceptMessage(msg);
				}
				return;
			}

			//
			// Reliable unordered
			//
			if (msg.m_sequenceChannel == NetChannel.ReliableUnordered)
			{
				// send acknowledge (even if duplicate)
				m_acknowledgesToSend.Enqueue((chanNr << 16) | msg.m_sequenceNumber);

				if (isDuplicate)
				{
					m_statistics.CountDuplicateMessage(msg);
					m_owner.LogVerbose("Rejecting duplicate " + msg);
					return; // reject duplicates
				}

				// It's good; add message
				AcceptMessage(msg);

				return;
			}

			ushort nextSeq = (ushort)(seqNr + 1);

			if (chanNr < (int)NetChannel.ReliableInOrder1)
			{
				//
				// Sequenced
				//
				if (relation < 0)
				{
					// late sequenced message
					m_statistics.CountDroppedSequencedMessage();
					m_owner.LogVerbose("Dropping late sequenced " + msg);
					return;
				}

				// It's good; add message
				AcceptMessage(msg);

				m_nextExpectedSequence[chanNr] = nextSeq;
				return;
			}
			else
			{
				//
				// Ordered
				// 

				// send ack (regardless)
				m_acknowledgesToSend.Enqueue((chanNr << 16) | msg.m_sequenceNumber);

				if (relation < 0)
				{
					// late ordered message
#if DEBUG
					if (!isDuplicate)
						m_owner.LogWrite("Ouch, weird! Late ordered message that's NOT a duplicate?! seqNr: " + seqNr + " expecting: " + m_nextExpectedSequence[chanNr]);
#endif
					// must be duplicate
					m_owner.LogVerbose("Dropping duplicate message " + seqNr);
					m_statistics.CountDuplicateMessage(msg);
					return; // rejected; don't advance next expected
				}

				if (relation > 0)
				{
					// early message; withhold ordered
					m_owner.LogVerbose("Withholding " + msg + " (expecting " + m_nextExpectedSequence[chanNr] + ")");
					m_withheldMessages.Add(msg);
					return; // return without advancing next expected
				}

				// It's right on time!
				AcceptMessage(msg);

				// ordered; release other withheld messages?
				bool released = false;
				do
				{
					released = false;
					foreach (NetMessage wm in m_withheldMessages)
					{
						if (wm.m_sequenceNumber == nextSeq)
						{
							m_owner.LogVerbose("Releasing withheld message " + wm);
							AcceptMessage(wm);
							nextSeq++;
							if (nextSeq >= NetConstants.NumSequenceNumbers)
								nextSeq -= NetConstants.NumSequenceNumbers;
							released = true;
							break;
						}
					}
				} while (released);
			}

			// Common to Sequenced and Ordered

			m_nextExpectedSequence[chanNr] = nextSeq;

			return;
		}

		internal void HandleSystemMessage(NetMessage msg, double now)
		{
			msg.m_data.Position = 0;
			NetSystemType sysType = (NetSystemType)msg.m_data.ReadByte();
			NetMessage response = null;
			switch (sysType)
			{
				case NetSystemType.Disconnect:
					if (m_status == NetConnectionStatus.Disconnected)
						return;
					Disconnect(msg.m_data.ReadString(), 0.75f + ((float)m_currentAvgRoundtrip * 4), false);
					break;
				case NetSystemType.Connect:

					// finalize disconnect if it's in process
					if (m_status == NetConnectionStatus.Disconnecting)
						FinalizeDisconnect();

					// send response; even if connected
					response = m_owner.CreateSystemMessage(NetSystemType.ConnectResponse);
					m_unsentMessages.Enqueue(response);

					if (m_status != NetConnectionStatus.Connecting)
						SetStatus(NetConnectionStatus.Connecting, "Connecting...");

					m_handshakeInitiated = now;
					break;
				case NetSystemType.ConnectResponse:
					if (m_status != NetConnectionStatus.Connecting && m_status != NetConnectionStatus.Connected)
					{
						m_owner.LogWrite("Received connection response but we're not connecting...");
						return;
					}

					// Send connectionestablished
					response = m_owner.CreateSystemMessage(NetSystemType.ConnectionEstablished);
					m_unsentMessages.Enqueue(response);

					// send first ping 250ms after connected
					m_lastSentPing = now - m_owner.Configuration.PingFrequency + 0.1 + (NetRandom.Instance.NextFloat() * 0.25f);
					m_statistics.Reset();
					SetInitialAveragePing(now - m_handshakeInitiated);
					SetStatus(NetConnectionStatus.Connected, "Connected");
					break;
				case NetSystemType.ConnectionEstablished:
					if (m_status != NetConnectionStatus.Connecting)
					{
						if ((m_owner.m_enabledMessageTypes & NetMessageType.BadMessageReceived) == NetMessageType.BadMessageReceived)
							m_owner.NotifyApplication(NetMessageType.BadMessageReceived, "Received connection response but we're not connecting...", this);
						return;
					}
					// send first ping 100-350ms after connected
					m_lastSentPing = now - m_owner.Configuration.PingFrequency + 0.1 + (NetRandom.Instance.NextFloat() * 0.25f);
					m_statistics.Reset();
					SetInitialAveragePing(now - m_handshakeInitiated);
					SetStatus(NetConnectionStatus.Connected, "Connected");
					break;
				case NetSystemType.Ping:
					// also accepted as ConnectionEstablished
					if (m_isInitiator == false && m_status == NetConnectionStatus.Connecting)
					{
						m_owner.LogWrite("Received ping; interpreted as ConnectionEstablished");
						m_statistics.Reset();
						SetInitialAveragePing(now - m_handshakeInitiated);
						SetStatus(NetConnectionStatus.Connected, "Connected");
					}

					//LogWrite("Received ping; sending pong...");
					SendPingPong(now, NetSystemType.Pong);
					break;
				case NetSystemType.Pong:
					double twoWayLatency = now - m_lastSentPing;
					if (twoWayLatency < 0)
						break;
					ReceivedPong(twoWayLatency, msg);
					break;
				default:
					m_owner.LogWrite("Undefined behaviour in NetConnection for system message " + sysType);
					break;
			}
		}

		/// <summary>
		/// Disconnects from remote host; lingering for 'lingerSeconds' to allow packets in transit to arrive
		/// </summary>
		public void Disconnect(string reason, float lingerSeconds)
		{
			Disconnect(reason, lingerSeconds, true);
		}

		internal void Disconnect(string reason, float lingerSeconds, bool sendGoodbye)
		{
			if (m_status == NetConnectionStatus.Disconnected)
				return;

			if (sendGoodbye)
			{
				NetBuffer scratch = m_owner.m_scratchBuffer;
				scratch.Reset();
				scratch.Write(string.IsNullOrEmpty(reason) ? "" : reason);
				m_owner.SendSingleUnreliableSystemMessage(
					NetSystemType.Disconnect,
					scratch,
					m_remoteEndPoint
				);
			}

			if (lingerSeconds <= 0)
			{
				SetStatus(NetConnectionStatus.Disconnected, reason);
				FinalizeDisconnect();
				m_futureClose = double.MaxValue;
				m_futureDisconnectReason = null;
			}
			else
			{
				SetStatus(NetConnectionStatus.Disconnecting, reason);
				m_futureClose = NetTime.Now + lingerSeconds;
				m_futureDisconnectReason = reason;
			}
		}
	}
}
