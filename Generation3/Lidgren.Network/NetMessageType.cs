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

namespace Lidgren.Network
{
	// public visible subset of NetMessageType

	/// <summary>
	/// How the library deals with dropped and delayed messages
	/// </summary>
	public enum NetDeliveryMethod : byte
	{
		Unknown = 0,
		Unreliable = 2,
		UnreliableSequenced = 3,
		ReliableUnordered = 36,
		ReliableSequenced = 37,
		ReliableOrdered = 69,
	}

	internal enum NetMessageLibraryType : byte
	{
		Error = 0,
		KeepAlive = 1,
		Ping = 2, // used for RTT calculation
		Pong = 3, // used for RTT calculation
		Connect = 4,
		ConnectResponse = 5,
		ConnectionEstablished = 6,
		Disconnect = 7,
		Discovery = 8,
		DiscoveryResponse = 9,
		NatIntroduction = 10,
	}

	internal enum NetMessageType : byte
	{
		Error = 0,

		Library = 1, // NetMessageLibraryType byte follows

		UserUnreliable = 2,

		UserSequenced = 3,
		// 4 to 35 = UserSequenced 0 to 31

		UserReliableUnordered = 36,

		UserReliableSequenced = 37,
		// 37 to 69 = UserReliableSequenced 0 to 31

		UserReliableOrdered = 69,
		// 69 to 100 = UserReliableOrdered 0 to 31
	}
}