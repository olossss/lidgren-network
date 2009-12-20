using System;
using System.Collections.Generic;

using Lidgren.Network2;
using System.Threading;

namespace ChatServer
{
	class Program
	{
		static void Main(string[] args)
		{
			NetPeerConfiguration config = new NetPeerConfiguration("chatapp");
			config.Port = 14242;

			NetServer server = new NetServer(config);
			server.Initialize();

			while (!Console.KeyAvailable)
			{
				NetIncomingMessage msg;
				while((msg = server.ReadMessage()) != null)
				{
					switch (msg.MessageType)
					{
						case NetIncomingMessageType.VerboseDebugMessage:
						case NetIncomingMessageType.DebugMessage:
						case NetIncomingMessageType.WarningMessage:
						case NetIncomingMessageType.ErrorMessage:
							string str = msg.ReadString();
							Console.WriteLine(str);
							break;

						case NetIncomingMessageType.StatusChanged:
							NetConnectionStatus status = (NetConnectionStatus)msg.ReadInt32();
							Console.WriteLine(msg.SenderConnection.ToString() + " new status: " + status);
							break;

						case NetIncomingMessageType.UnconnectedData:
							string ustr = msg.ReadString();
							Console.WriteLine(msg.SenderEndPoint + " wrote unconnected: " + ustr);
							break;

						case NetIncomingMessageType.Data:

							string astr = msg.ReadString();

							NetOutgoingMessage reply = server.CreateMessage();
							reply.Write(msg.SenderEndPoint.ToString() + " wrote: " + astr);
							server.SendMessage(reply, server.Connections, NetMessageChannel.ReliableOrdered1, NetMessagePriority.Normal);
							break;

						default:
							Console.WriteLine("Not supported: " + msg.MessageType);
							break;
					}
				}
				Thread.Sleep(1);
			}

			server.Shutdown("Application exiting");

			Thread.Sleep(100); // let disconnect make it out the door
		}
	}
}
