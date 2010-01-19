using System;
using System.Collections.Generic;

namespace Lidgren.Network2
{
	public sealed class NetPeerStatistics
	{
		internal int m_sentPackets;
		internal int m_receivedPackets;

		internal int m_sentBytes;
		internal int m_receivedBytes;

		internal NetPeerStatistics()
		{
			Reset();
		}

		internal void Reset()
		{
			m_sentPackets = 0;
			m_receivedPackets = 0;
		}

		/// <summary>
		/// Gets the number of sent packets since the NetPeer was initialized
		/// </summary>
		public int SentPackets { get { return m_sentPackets; } }

		/// <summary>
		/// Gets the number of received packets since the NetPeer was initialized
		/// </summary>
		public int ReceivedPackets { get { return m_receivedPackets; } }

		/// <summary>
		/// Gets the number of sent bytes since the NetPeer was initialized
		/// </summary>
		public int SentBytes { get { return m_sentBytes; } }

		/// <summary>
		/// Gets the number of received bytes since the NetPeer was initialized
		/// </summary>
		public int ReceivedBytes { get { return m_receivedBytes; } }
	}
}
