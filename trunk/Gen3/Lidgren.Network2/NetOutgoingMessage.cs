using System;
using System.Collections.Generic;

namespace Lidgren.Network2
{
	public sealed class NetOutgoingMessage
	{
		private bool m_isSent;

		/// <summary>
		/// Returns true if this message has been passed to SendMessage() already
		/// </summary>
		public bool IsSent { get { return m_isSent; } }


	}
}
