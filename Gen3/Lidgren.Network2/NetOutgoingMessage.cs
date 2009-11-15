using System;
using System.Collections.Generic;

namespace Lidgren.Network2
{
	public sealed partial class NetOutgoingMessage
	{
		private bool m_isSent;
		private NetMessagePriority m_priority;

		/// <summary>
		/// Gets or sets the priority of this message. Higher priority messages are sent before lower ones.
		/// </summary>
		public NetMessagePriority Priority { get { return m_priority; } set { m_priority = value; } }

		/// <summary>
		/// Returns true if this message has been passed to SendMessage() already
		/// </summary>
		public bool IsSent { get { return m_isSent; } }

		internal NetOutgoingMessage()
		{
			Reset();
		}

		internal void Reset()
		{
			m_isSent = false;
			m_priority = NetMessagePriority.Normal;
		}
	}
}
