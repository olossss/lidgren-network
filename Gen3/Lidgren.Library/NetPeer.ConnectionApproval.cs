using System;
using System.Net;
using System.Collections.Generic;

namespace Lidgren.Network
{
	internal enum PendingConnectionStatus
	{
		NotPending = 0,
		Pending,
		Approved,
		Denied,
	}

	public partial class NetPeer
	{
		private List<NetConnection> m_pendingConnections;

		private void AddPendingConnection(NetConnection conn)
		{
			if (m_pendingConnections == null)
				m_pendingConnections = new List<NetConnection>();
			m_pendingConnections.Add(conn);
			conn.m_pendingStatus = PendingConnectionStatus.Pending;

			NetIncomingMessage inc = CreateIncomingMessage(NetIncomingMessageType.ConnectionApproval, 0);
			inc.m_data = conn.m_remoteHailData;
			inc.m_bitLength = (inc.m_data == null ? 0 : inc.m_data.Length * 8);
			inc.m_senderConnection = conn;

			ReleaseMessage(inc);
		}

		private void CheckPendingConnections()
		{
			if (m_pendingConnections == null || m_pendingConnections.Count < 1)
				return;

			foreach (NetConnection conn in m_pendingConnections)
			{
				switch (conn.m_pendingStatus)
				{
					case PendingConnectionStatus.Pending:
						if (NetTime.Now > conn.m_connectInitationTime + 10.0)
						{
							LogWarning("Pending connection still in pending state after 10 seconds; forgot to Approve/Deny?");
							m_pendingConnections.Remove(conn);
							return;
						}
						break;
					case PendingConnectionStatus.Approved:
						// accept connection
						AcceptConnection(conn);
						m_pendingConnections.Remove(conn);
						return;
					case PendingConnectionStatus.Denied:
						// send disconnected
						NetOutgoingMessage bye = CreateMessage(string.IsNullOrEmpty(conn.m_pendingDenialReason) ? conn.m_pendingDenialReason.Length : 0);
						bye.m_type = NetMessageType.LibraryDisconnect;
						bye.Write(string.IsNullOrEmpty(conn.m_pendingDenialReason) ? "" : conn.m_pendingDenialReason);
						EnqueueUnconnectedMessage(bye, conn.m_remoteEndPoint);
						m_pendingConnections.Remove(conn);
						return;
				}
			}
		}
	}
}
