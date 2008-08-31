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

			client.SimulatedMinimumLatency = 0.05f;
			client.SimulatedLatencyVariance = 0.025f;
			client.SimulatedLoss = 0.03f;

			// wait half a second to allow server to start up in Visual Studio
			Thread.Sleep(500);

			// create a buffer to read data into
			NetBuffer buffer = client.CreateBuffer();

			// connect to localhost
			client.Connect("localhost", 14242, new byte[] { 42 }); // send a single byte, 42, as hail data

			// enable some library messages
			client.SetMessageTypeEnabled(NetMessageType.BadMessageReceived, true);
			client.SetMessageTypeEnabled(NetMessageType.ConnectionRejected, true);

			// create a stopwatch
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
							//
							// These three types of messages all contain a string in the buffer; display it.
							//
							Console.WriteLine(buffer.ReadString());
							break;
						case NetMessageType.Data:
						default:
							//
							// For this application; server doesn't send any data... so Data messages are unhandled
							//
							Console.WriteLine("Unhandled: " + type + " " + buffer.ToString());
							break;
					}
				}

				// send a message every second
				if (client.Status == NetConnectionStatus.Connected && sw.Elapsed.TotalSeconds >= 1)
				{
					loops++;
					//Console.WriteLine("Sending message #" + loops);
					Console.Title = "Client; Messages sent: " + loops;

					NetBuffer send = client.CreateBuffer();
					send.Write("Message #" + loops);
					client.SendMessage(send, NetChannel.ReliableUnordered);

					sw.Reset();
					sw.Start();
				}

				Thread.Sleep(5);
			}

			// clean shutdown
			client.Shutdown("Application exiting");
		}
	}
}
