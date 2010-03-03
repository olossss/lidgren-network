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
			peer.Start(); // needed for initialization

			System.Threading.Thread.Sleep(50);

			Console.WriteLine("MAC is " + NetUtility.GetMacAddress());

			ReadWriteTests.Run(peer);

			NetQueueTests.Run();

			MiscTests.Run(peer);

			peer.Shutdown("bye");

			Console.ReadKey();
		}
	}
}
