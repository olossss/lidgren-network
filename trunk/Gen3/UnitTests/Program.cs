using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lidgren.Network;

namespace UnitTests
{
	class Program
	{
		static void Main(string[] args)
		{
			NetPeerConfiguration config = new NetPeerConfiguration("unittests");
			NetPeer peer = new NetPeer(config);

			ReadWriteTests.Run(peer);

			Console.ReadKey();
		}
	}
}
