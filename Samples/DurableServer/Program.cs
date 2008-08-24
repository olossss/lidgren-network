using System;
using System.Collections.Generic;
using System.Text;
using Lidgren.Network;
using System.Threading;

namespace DurableServer
{
	class Program
	{
		static void Main(string[] args)
		{
			NetConfiguration config = new NetConfiguration("durable");
			config.MaxConnections = 128;
			config.Port = 14242;
			NetServer server = new NetServer(config);

			server.SimulatedMinimumLatency = 0.05f;
			server.SimulatedLatencyVariance = 0.025f;
			server.SimulatedLoss = 0.03f;

			server.Start();

			NetBuffer buffer = server.CreateBuffer();

			int expected = 1;

			Console.WriteLine("Press any key to quit");
			while (!Console.KeyAvailable)
			{
				NetMessageType type;
				NetConnection sender;
				if (server.ReadMessage(buffer, out type, out sender))
				{
					switch (type)
					{
						case NetMessageType.StatusChanged:
							Console.WriteLine("New status: " + sender.Status + " (" + buffer.ReadString() + ")");
							break;
						case NetMessageType.BadMessageReceived:
						case NetMessageType.ConnectionRejected:
						case NetMessageType.DebugMessage:
							//
							// All these types of messages all contain a single string in the buffer; display it
							//
							Console.WriteLine(buffer.ReadString());
							break;
						case NetMessageType.Data:
							string str = buffer.ReadString();

							// parse it
							int nr = Int32.Parse(str.Substring(9));

							if (nr != expected)
							{
								Console.WriteLine("Warning! Expected " + expected + "; received " + nr);
							}
							else
							{
								expected++;
								Console.Title = "Server; received " + nr + " messages";
							}

							break;
						default:
							Console.WriteLine("Unhandled: " + type + " " + buffer.ToString());
							break;
					}
				}
				Thread.Sleep(5);
			}

			// clean shutdown
			server.Shutdown("Application exiting");
		}
	}
}
