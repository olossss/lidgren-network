using System;
using System.Collections.Generic;
using System.Text;
using Lidgren.Network;
using System.Threading;

namespace ChatClient
{
	class Program
	{
		static void Main(string[] args)
		{
			// create a client with a default configuration
			NetConfiguration config = new NetConfiguration("chatApp");
			NetClient client = new NetClient(config);
			client.Start();

			// Wait 1/4 of a second to allow server to start up if run via Visual Studio
			Thread.Sleep(250);

			// Emit discovery signal
			client.DiscoverLocalServers(14242);

			// create a buffer to read data into
			NetBuffer buffer = client.CreateBuffer();

			// current input string
			string input = "";

			// keep running until the user presses a key
			Console.WriteLine("Type 'quit' to exit client");
			bool keepRunning = true;
			while (keepRunning)
			{
				NetMessageType type;

				// check if any messages has been received
				while (client.ReadMessage(buffer, out type))
				{
					switch (type)
					{
						case NetMessageType.ServerDiscovered:
							// just connect to any server found!
							client.Connect(buffer.ReadIPEndPoint());
							break;
						case NetMessageType.DebugMessage:
							Console.WriteLine(buffer.ReadString());
							break;
						case NetMessageType.StatusChanged:
							Console.WriteLine("New status: " + client.Status + " (" + buffer.ReadString() + ")");
							break;
						case NetMessageType.Data:
							// The server sent this data!
							string msg = buffer.ReadString();
							Console.WriteLine(msg);
							break;
					}
				}

				while (Console.KeyAvailable)
				{
					ConsoleKeyInfo ki = Console.ReadKey();
					if (ki.Key == ConsoleKey.Enter)
					{
						if (!string.IsNullOrEmpty(input))
						{
							if (input == "quit")
							{
								// exit application
								keepRunning = false;
							}
							else
							{
								// Send chat message
								NetBuffer sendBuffer = new NetBuffer();
								sendBuffer.Write(input);
								client.SendMessage(sendBuffer, NetChannel.ReliableInOrder1);
								input = "";
							}
						}
					}
					else
					{
						input += ki.KeyChar;
					}
				}

				Thread.Sleep(1);
			}

			client.Shutdown("Application exiting");
		}
	}
}
