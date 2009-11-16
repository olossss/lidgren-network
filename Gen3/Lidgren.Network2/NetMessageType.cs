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

		UserSequenced1 = 15,
		UserSequenced2 = 16,
		UserSequenced3 = 17,
		UserSequenced4 = 18,
		UserSequenced5 = 19,
		UserSequenced6 = 20,
		UserSequenced7 = 21,
		UserSequenced8 = 22,
		UserSequenced9 = 23,
		UserSequenced10 = 24,
		UserSequenced11 = 25,
		UserSequenced12 = 26,
		UserSequenced13 = 27,
		UserSequenced14 = 28,
		UserSequenced15 = 29,
		UserSequenced16 = 30,

		UserReliableUnordered = 31,

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