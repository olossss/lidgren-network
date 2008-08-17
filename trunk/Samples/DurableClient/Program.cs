using System;
using System.Collections.Generic;
using System.Text;
using Lidgren.Network;
using System.Threading;
using System.Diagnostics;

namespace DurableClient
{
	class Program
	{
		static void Main(string[] args)
		{
			NetConfiguration config = new NetConfiguration("durable");
			NetClient client = new NetClient(config);

			Thread.Sleep(500); // wait for server to start up in VS

			NetBuffer buffer = client.CreateBuffer();

			client.Connect("localhost", 14242, null);

			client.SetMessageTypeEnabled(NetMessageType.BadMessageReceived, true);
			client.SetMessageTypeEnabled(NetMessageType.ConnectionRejected, true);

			Stopwatch sw = new Stopwatch();
			sw.Start();
			int loops = 0;

			while (!Console.KeyAvailable)
			{
				NetMessageType type;
				if (client.ReadMessage(buffer, out type))
				{
					switch (type)
					{
						case NetMessageType.StatusChanged:
							Console.WriteLine("New status: " + client.Status + " (" + buffer.ReadString() + ")");
							break;
						case NetMessageType.BadMessageReceived:
						case NetMessageType.ConnectionRejected:
						case NetMessageType.DebugMessage:
							Console.WriteLine(buffer.ReadString());
							break;
						case NetMessageType.Data:
						default:
							Console.WriteLine("Unhandled: " + type + " " + buffer.ToString());
							break;
					}
				}

				if (sw.Elapsed.TotalSeconds >= 60)
				{
					loops++;
					Console.WriteLine("Sending minute #" + loops);

					NetBuffer send = client.CreateBuffer();
					send.Write("Minute #" + loops);
					client.SendMessage(send, NetChannel.ReliableUnordered);

					sw.Reset();
					sw.Start();
				}

				Thread.Sleep(5);
			}

			client.Shutdown("Application exiting");
		}
	}
}
