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
			server.Start();

			NetBuffer buffer = server.CreateBuffer();

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
						case NetMessageType.Data:
							//
							// All these types of messages all contain a single string in the buffer; display it
							//
							Console.WriteLine(buffer.ReadString());
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
