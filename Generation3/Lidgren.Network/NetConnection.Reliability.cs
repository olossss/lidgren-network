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

namespace Lidgren.Network
{
	public partial class NetConnection
	{
		private ushort[] m_nextSendSequenceNumber;

		private List<NetOutgoingMessage> m_storedMessages; // naïve! replace by something better

		private void InitializeReliability()
		{
			int num = ((int)NetMessageType.UserReliableOrdered + NetConstants.kNetChannelsPerDeliveryMethod) - (int)NetMessageType.UserSequenced;
			m_nextSendSequenceNumber = new ushort[num];
			m_storedMessages = new List<NetOutgoingMessage>();
		}

		internal ushort GetSendSequenceNumber(NetMessageType tp)
		{
			m_owner.VerifyNetworkThread();
			int slot = (int)tp - (int)NetMessageType.UserSequenced;
			return m_nextSendSequenceNumber[slot]++;
		}

		// returns true if message should be rejected
		internal bool ReceivedSequencedMessage(NetMessageType mtp, ushort seqNr)
		{
			throw new NotImplementedException();
			// check seqNr vs m_lastReceivedSequenced - updating it or returning true (reject)
		}

		private void StoreReliableMessage(NetOutgoingMessage msg)
		{
			m_owner.VerifyNetworkThread();
			m_storedMessages.Add(msg);
		}
	}
}
