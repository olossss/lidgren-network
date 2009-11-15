using System;

namespace Lidgren.Network2
{
	internal enum NetMessageType : byte
	{
		Error = 0,

		LibraryAcknowledge = 1,
		LibraryPing = 2,
		LibraryPong = 3,
		LibraryConnect = 4,
		LibraryConnectResponse = 5,
		LibraryConnectionEstablished = 6,
		LibraryConnectionRejected = 7,
		LibraryDisconnect = 8,
		LibraryDiscovery = 9,
		LibraryDiscoveryResponse = 10,
		LibraryNatIntroduction = 11,

		Unused1 = 12,
		Unused2 = 13,

		UserUnreliable = 14,
		UserReliableUnordered = 15,

		UserSequenced1 = 16,
		UserSequenced2 = 17,
		UserSequenced3 = 18,
		UserSequenced4 = 19,
		UserSequenced5 = 20,
		UserSequenced6 = 21,
		UserSequenced7 = 22,
		UserSequenced8 = 23,
		UserSequenced9 = 24,
		UserSequenced10 = 25,
		UserSequenced11 = 26,
		UserSequenced12 = 27,
		UserSequenced13 = 28,
		UserSequenced14 = 29,
		UserSequenced15 = 30,
		UserSequenced16 = 31,

		UserReliableSequenced1 = 32,
		UserReliableSequenced2 = 33,
		UserReliableSequenced3 = 34,
		UserReliableSequenced4 = 35,
		UserReliableSequenced5 = 36,
		UserReliableSequenced6 = 37,
		UserReliableSequenced7 = 38,
		UserReliableSequenced8 = 39,
		UserReliableSequenced9 = 40,
		UserReliableSequenced10 = 41,
		UserReliableSequenced11 = 42,
		UserReliableSequenced12 = 43,
		UserReliableSequenced13 = 44,
		UserReliableSequenced14 = 45,
		UserReliableSequenced15 = 46,
		UserReliableSequenced16 = 47,

		UserReliableOrdered1 = 48,
		UserReliableOrdered2 = 49,
		UserReliableOrdered3 = 50,
		UserReliableOrdered4 = 51,
		UserReliableOrdered5 = 52,
		UserReliableOrdered6 = 53,
		UserReliableOrdered7 = 54,
		UserReliableOrdered8 = 55,
		UserReliableOrdered9 = 56,
		UserReliableOrdered10 = 57,
		UserReliableOrdered11 = 58,
		UserReliableOrdered12 = 59,
		UserReliableOrdered13 = 60,
		UserReliableOrdered14 = 61,
		UserReliableOrdered15 = 62,
		UserReliableOrdered16 = 63
	}
}