using System;

namespace Lidgren.Network
{
	// public visible subset of NetMessageType
	public enum NetDeliveryMethod : byte
	{
		Unreliable = 16,
		UnreliableSequenced = 32,
		ReliableUnordered = 96,
		ReliableSequenced = 97,
		ReliableOrdered = 161,
	}

	internal enum NetMessageType : byte
	{
		Error = 0,

		LibraryKeepAlive = 1, // also used for explicit/forced ack
		LibraryConnect = 2,
		LibraryConnectResponse = 3,
		LibraryConnectionEstablished = 4,
		LibraryDisconnect = 5,
		LibraryDiscovery = 6,
		LibraryDiscoveryResponse = 7,
		LibraryNatIntroduction = 8,

		UserUnreliable = 16,

		UserSequenced = 32,
		// 32 to 95 = UserSequenced 0 to 63

		UserReliableUnordered = 96,

		UserReliableSequenced = 97,
		// 97 to 160 = UserReliableSequenced 0 to 63
		
		UserReliableOrdered = 161,
		// 161 to 224 = UserReliableOrdered 0 to 63
	}
}