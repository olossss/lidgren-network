using System;
using System.Collections.Generic;

namespace Lidgren.Network
{
	public partial class NetConnection
	{
		internal bool m_connectRequested;
		internal bool m_disconnectRequested;
		internal string m_disconnectByeMessage;
		internal bool m_connectionInitiator;
		internal double m_connectInitationTime; // regardless of initiator
		internal string m_hail;
		internal NetConnectionStatus m_status;

		internal void SetStatus(NetConnectionStatus status, string reason)
		{
			m_owner.VerifyNetworkThread();
	
			if (status == m_status)
				return;
			m_status = status;
			if (reason == null)
				reason = string.Empty;
			if (m_owner.m_configuration.IsMessageTypeEnabled(NetIncomingMessageType.StatusChanged))
			{
				NetIncomingMessage info = m_owner.CreateIncomingMessage(NetIncomingMessageType.StatusChanged, 4 + reason.Length + (reason.Length > 126 ? 2 : 1));
				info.m_senderConnection = this;
				info.m_senderEndPoint = m_remoteEndPoint;
				info.Write((byte)m_status);
				info.Write(reason);
				m_owner.ReleaseMessage(info);
			}
		}

		private void SendConnect()
		{
			m_owner.VerifyNetworkThread();

			switch (m_status)
			{
				case NetConnectionStatus.Connected:
					// reconnect
					m_disconnectByeMessage = "Reconnecting";
					ExecuteDisconnect(NetMessagePriority.High, true);
					FinishDisconnect();
					break;
				case NetConnectionStatus.Connecting:
				case NetConnectionStatus.None:
					// just send connect, regardless of who was previous initiator
					break;
				case NetConnectionStatus.Disconnected:
					throw new Exception("This connection is Disconnected; spent. A new one should have been created");
					break;
				case NetConnectionStatus.Disconnecting:
					// let disconnect finish first
					return;
			}

			m_connectRequested = false;
			SetStatus(NetConnectionStatus.Connecting, "Connecting");

			// start handshake

			int len = 2 + m_owner.m_macAddressBytes.Length;
			NetOutgoingMessage om = m_owner.CreateMessage(len);
			om.m_type = NetMessageType.LibraryConnect;
			om.Write(m_owner.m_configuration.AppIdentifier);
			//om.Write((byte)m_owner.m_macAddressBytes.Length);
			//om.Write(m_owner.m_macAddressBytes);
			if (m_hail == null)
				m_hail = string.Empty;
			om.Write(m_hail);

			m_owner.LogVerbose("Sending Connect");
			
			// don´t use high prio, in case a disconnect message is in the queue too
			EnqueueOutgoingMessage(om, NetMessagePriority.Normal);

			m_connectInitationTime = NetTime.Now;
			return;
		}

		internal void ExecuteDisconnect(NetMessagePriority prio, bool sendByeMessage)
		{
			m_owner.VerifyNetworkThread();

			if (m_status == NetConnectionStatus.Disconnected || m_status == NetConnectionStatus.None)
				return;

			if (sendByeMessage)
			{
				NetOutgoingMessage om = m_owner.CreateMessage(0);
				om.m_type = NetMessageType.LibraryDisconnect;
				if (m_disconnectByeMessage == null)
					m_disconnectByeMessage = string.Empty;
				om.Write(m_disconnectByeMessage);

				EnqueueOutgoingMessage(om, prio);
			}

			m_owner.LogVerbose("Executing Disconnect(" + m_disconnectByeMessage + ")");
		
			return;
		}

		private void FinishDisconnect()
		{
			if (m_status == NetConnectionStatus.Disconnected || m_status == NetConnectionStatus.None)
				return;

			m_owner.LogVerbose("Finishing Disconnect(" + m_disconnectByeMessage + ")");

			ResetSlidingWindow();

			SetStatus(NetConnectionStatus.Disconnected, m_disconnectByeMessage);
			m_disconnectByeMessage = null;
			m_disconnectRequested = false;
			m_connectionInitiator = false;
		}

		private void HandleIncomingHandshake(NetMessageType mtp, byte[] payload, int payloadBytesLength)
		{
			m_owner.VerifyNetworkThread();

			switch (mtp)
			{
				case NetMessageType.LibraryConnect:
					m_owner.LogError("NetConnection.HandleIncomingHandshake() passed LibraryConnect!?");
					break;
				case NetMessageType.LibraryConnectResponse:
					if (!m_connectionInitiator)
					{
						m_owner.LogError("NetConnection.HandleIncomingHandshake() passed LibraryConnectResponse, but we're not initiator!");
						// weird, just drop it
						return;
					}

					if (m_status == NetConnectionStatus.Connecting)
					{
						// excellent, handshake making progress; send connectionestablished
						SetStatus(NetConnectionStatus.Connected, "Connected");

						m_owner.LogVerbose("Sending LibraryConnectionEstablished");
						NetOutgoingMessage ce = m_owner.CreateMessage(0);
						ce.m_type = NetMessageType.LibraryConnectionEstablished;
						EnqueueOutgoingMessage(ce, NetMessagePriority.High);

						// setup initial ping estimation
						InitializeLatency((float)(NetTime.Now - m_connectInitationTime));
						return;
					}

					m_owner.LogWarning("NetConnection.HandleIncomingHandshake() passed " + mtp + ", but status is " + m_status);
					break;
				case NetMessageType.LibraryConnectionEstablished:
					if (!m_connectionInitiator && m_status == NetConnectionStatus.Connecting)
					{
						// handshake done
						if (!m_isPingInitialized)
							InitializeLatency((float)(NetTime.Now - m_connectInitationTime));

						SetStatus(NetConnectionStatus.Connected, "Connected");
						return;
					}

					m_owner.LogWarning("NetConnection.HandleIncomingHandshake() passed " + mtp + ", but initiator is " + m_connectionInitiator + " and status is " + m_status);
					break;
				case NetMessageType.LibraryDisconnect:
					// extract bye message
					NetIncomingMessage im = m_owner.CreateIncomingMessage(NetIncomingMessageType.Data, payload, payloadBytesLength);
					m_disconnectByeMessage = im.ReadString();
					m_disconnectRequested = true;
					//ExecuteDisconnect(NetMessagePriority.Low, false);
					break;
				default:
					// huh?
					throw new NotImplementedException();
			}
		}
	}
}
