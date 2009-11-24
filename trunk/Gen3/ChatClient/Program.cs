using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lidgren.Network2;
using System.Threading;

namespace ChatClient
{
	class Program
	{
		static void Main(string[] args)
		{
			NetPeerConfiguration config = new NetPeerConfiguration("chatapp");

			NetServer client = new NetServer(config);
			client.Initialize();

			Thread.Sleep(1000); // let server start up

			client.Connect("localhost", 14242);

			while (!Console.KeyAvailable)
			{
				NetIncomingMessage msg;
				while ((msg = client.ReadMessage()) != null)
				{
					switch (msg.MessageType)
					{
						case NetIncomingMessageType.VerboseDebugMessage:
						case NetIncomingMessageType.DebugMessage:
						case NetIncomingMessageType.WarningMessage:
						case NetIncomingMessageType.ErrorMessage:
							Console.WriteLine(msg.ReadString());
							break;

						case NetIncomingMessageType.StatusChanged:
							Console.WriteLine(msg.SenderConnection.ToString() + " new status: " + (NetConnectionStatus)msg.ReadInt32());
							break;

						default:
							Console.WriteLine("Not supported: " + msg.MessageType);
							break;
					}
				}
				Thread.Sleep(1);
			}

			client.Shutdown("Application exiting");

			Thread.Sleep(100); // let disconnect make it out the door
		}
	}
}
