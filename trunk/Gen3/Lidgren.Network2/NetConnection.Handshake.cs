using System;
using System.Collections.Generic;

namespace Lidgren.Network2
{
	public partial class NetConnection
	{
		internal bool m_connectRequested;
		internal bool m_disconnectRequested;
		internal string m_disconnectByeMessage;
		internal bool m_connectionInitiator;
		internal string m_hail;
		private NetConnectionStatus m_status;

		// runs on network thread
		private void SendConnect()
		{
			switch (m_status)
			{
				case NetConnectionStatus.Connected:
					// reconnect
					m_disconnectByeMessage = "Reconnecting";
					SendDisconnect();
					break;
				case NetConnectionStatus.Connecting:
				case NetConnectionStatus.Disconnected:
					// just send connect, regardless of who was previous initiator
					break;
				case NetConnectionStatus.Disconnecting:
					// let disconnect finish first
					return;
			}

			m_connectRequested = false;

			// start handshake

			int len = 2 + m_owner.m_macAddressBytes.Length;
			NetOutgoingMessage om = m_owner.CreateMessage(len);
			om.m_type = NetMessageType.LibraryConnect;
			om.Write((byte)m_owner.m_macAddressBytes.Length);
			om.Write(m_owner.m_macAddressBytes);
			if (m_hail == null)
				m_hail = string.Empty;
			om.Write(m_hail);

			m_owner.LogVerbose("Sending Connect");
			
			// don´t use high prio, in case a disconnect message is in the queue too
			EnqueueOutgoingMessage(om, NetMessagePriority.Normal);
			return;
		}

		private void SendDisconnect()
		{
			NetOutgoingMessage om = m_owner.CreateMessage(0);
			om.m_type = NetMessageType.LibraryDisconnect;
			if (m_disconnectByeMessage == null)
				m_disconnectByeMessage = string.Empty;
			om.Write(m_disconnectByeMessage);

			m_owner.LogVerbose("Sending disconnect(" + m_disconnectByeMessage + ")");

			// let high prio stuff slip past before disconnecting
			EnqueueOutgoingMessage(om, NetMessagePriority.Normal);
			return;
		}

		private void HandleIncomingHandshake(NetMessageType mtp, byte[] payload, int payloadBytesLength)
		{

			throw new NotImplementedException();
		}
	}
}
