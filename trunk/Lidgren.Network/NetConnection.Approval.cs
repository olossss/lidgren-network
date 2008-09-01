using System;
using System.Collections.Generic;
using System.Text;

namespace Lidgren.Network
{
	public sealed partial class NetConnection
	{
		public void Approve()
		{
			if (m_approved == true)
				throw new NetException("Connection is already approved!");

			//
			// Continue connection phase
			//

			// Add connection
			m_approved = true;

			NetServer server = m_owner as NetServer;
			server.AddConnection(NetTime.Now, this);
		}

		public void Disapprove(string reason)
		{
			if (m_approved == true)
				throw new NetException("Connection is already approved!");

			if (!string.IsNullOrEmpty(reason))
			{
				NetBuffer bye = new NetBuffer();
				bye.Write(reason);
				m_owner.SendSingleUnreliableSystemMessage(
					NetSystemType.Disconnect,
					bye,
					m_remoteEndPoint,
					false
				);
			}

			// throw it away
		}
	}
}
