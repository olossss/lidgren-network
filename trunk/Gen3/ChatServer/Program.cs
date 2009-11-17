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

						case NetIncomingMessageType.Data:

							string astr = msg.ReadString();

							NetOutgoingMessage reply = server.CreateMessage();
							reply.Write(msg.SenderEndpoint.ToString() + " wrote: " + astr);
							server.SendToAll(reply, NetMessagePriority.Normal);
							break;

						default:
							Console.WriteLine("Not supported: " + msg.MessageType);
							break;
					}
				}
				Thread.Sleep(1);
			}

			server.Shutdown();
		}
	}
}
