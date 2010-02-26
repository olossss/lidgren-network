using System;
using System.Collections.Generic;
using System.Text;

namespace Lidgren.Network
{
	public sealed class NetConnectionStatistics
	{
		private NetConnection m_connection;

		internal int m_sentPackets;
		internal int m_receivedPackets;

		internal int m_sentBytes;
		internal int m_receivedBytes;

		internal NetConnectionStatistics(NetConnection conn)
		{
			m_connection = conn;
			Reset();
		}

		internal void Reset()
		{
			m_sentPackets = 0;
			m_receivedPackets = 0;
			m_sentBytes = 0;
			m_receivedBytes = 0;
		}

		/// <summary>
		/// Gets the number of sent packets for this connection
		/// </summary>
		public int SentPackets { get { return m_sentPackets; } }

		/// <summary>
		/// Gets the number of received packets for this connection
		/// </summary>
		public int ReceivedPackets { get { return m_receivedPackets; } }

		/// <summary>
		/// Gets the number of sent bytes for this connection
		/// </summary>
		public int SentBytes { get { return m_sentBytes; } }

		/// <summary>
		/// Gets the number of received bytes for this connection
		/// </summary>
		public int ReceivedBytes { get { return m_receivedBytes; } }

		public override string ToString()
		{
			StringBuilder bdr = new StringBuilder();
			bdr.AppendLine("Sent " + m_sentBytes + " bytes in " + m_sentPackets + " packets");
			bdr.AppendLine("Received " + m_receivedBytes + " bytes in " + m_receivedPackets + " packets");
			return bdr.ToString();
		}
	}
}
