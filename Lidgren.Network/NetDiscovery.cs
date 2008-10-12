using System;
using System.Collections.Generic;

using System.Net;

namespace Lidgren.Network
{
	internal static class NetDiscovery
	{
			/// <summary>
		/// Emit a discovery signal to a host or subnet
		/// </summary>
		internal static void SendDiscoveryRequest(NetBase netBase, IPEndPoint endPoint, bool useBroadcast)
		{
			if (!netBase.m_isBound)
				netBase.Start();

			string appIdent = netBase.m_config.ApplicationIdentifier;
			NetBuffer data = new NetBuffer(appIdent.Length + 8);

			// write app identifier
			data.Write(appIdent);

			// write netbase identifier to avoid self-discovery
			data.Write(netBase.m_randomIdentifier);

			netBase.LogWrite("Discovering " + endPoint.ToString() + "...");
			netBase.QueueSingleUnreliableSystemMessage(NetSystemType.Discovery, data, endPoint, useBroadcast);
		}

		internal static void SendDiscoveryResponse(NetBase netBase, IPEndPoint endPoint)
		{
			netBase.SendSingleUnreliableSystemMessage(
				NetSystemType.DiscoveryResponse,
				null,
				endPoint,
				false
			);
		}

		internal static NetMessage CreateMessageFromResponse(
			NetBase netBase,
			NetMessage message,
			IPEndPoint endPoint
		)
		{
			int payLen = message.m_data.LengthBytes;

			// DiscoveryResponse found
			if ((netBase.m_enabledMessageTypes & NetMessageType.ServerDiscovered) != NetMessageType.ServerDiscovered)
				return null; // disabled

			byte[] discoverData = new byte[payLen - 1];
			if (payLen > 1)
				Buffer.BlockCopy(message.m_data.Data, 1, discoverData, 0, payLen - 1);

			NetMessage resMsg = netBase.CreateMessage();
			resMsg.m_msgType = NetMessageType.ServerDiscovered;

			NetBuffer resBuf = netBase.CreateBuffer();
			resMsg.m_data = resBuf;

			// write sender, assume ipv4
			resBuf.Write(endPoint);
			resBuf.Write(discoverData);

			resBuf.Write(BitConverter.ToUInt32(endPoint.Address.GetAddressBytes(), 0));
			resBuf.Write(endPoint.Port);
			resBuf.Write(discoverData);

			return resMsg;
		}

		internal static bool VerifyIdentifiers(
			NetBase netBase,
			NetMessage message,
			IPEndPoint endPoint
		)
		{
			int payLen = message.m_data.LengthBytes;
			if (payLen < 13)
			{
				if ((netBase.m_enabledMessageTypes & NetMessageType.BadMessageReceived) == NetMessageType.BadMessageReceived)
					netBase.NotifyApplication(NetMessageType.BadMessageReceived, "Malformed Discovery message received from " + endPoint, null);
				return false;
			}
			
			// check app identifier
			string appIdent2 = message.m_data.ReadString();
			if (appIdent2 != netBase.m_config.ApplicationIdentifier)
			{
				if ((netBase.m_enabledMessageTypes & NetMessageType.BadMessageReceived) == NetMessageType.BadMessageReceived)
					netBase.NotifyApplication(NetMessageType.BadMessageReceived, "Discovery for different application identification received: " + appIdent2, null);
				return false;
			}

			// check netbase identifier
			byte[] nbid = message.m_data.ReadBytes(netBase.m_randomIdentifier.Length);
			if (NetUtility.CompareElements(nbid, netBase.m_randomIdentifier))
				return false; // don't respond to your own discovery request

			// it's ok 
			return true;
		}
	}
}
