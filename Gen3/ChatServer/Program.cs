using System;
using System.Collections.Generic;

using Lidgren.Network2;
using System.Threading;
using System.IO;
using System.Diagnostics;

namespace ChatServer
{
	class Program
	{
		static void Main(string[] args)
		{
			NetPeerConfiguration config = new NetPeerConfiguration("chatapp");
			config.EnableMessageType(NetIncomingMessageType.VerboseDebugMessage);
			config.Port = 14242;

			NetServer server = new NetServer(config);
			server.Start();

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
							Output(str);
							break;

						case NetIncomingMessageType.StatusChanged:
							NetConnectionStatus status = (NetConnectionStatus)msg.ReadInt32();
							string reason = msg.ReadString();
							Output("Status " + reason + " (" + status + ")");
							break;

						case NetIncomingMessageType.UnconnectedData:
							string ustr = msg.ReadString();
							Output(msg.SenderEndPoint + " wrote unconnected: " + ustr);
							break;

						case NetIncomingMessageType.Data:

							string astr = msg.ReadString();

							NetOutgoingMessage reply = server.CreateMessage(astr.Length + 1);
							reply.Write(msg.SenderEndPoint.ToString() + " wrote: " + astr);
							server.SendMessage(reply, server.Connections, NetMessageChannel.ReliableOrdered1, NetMessagePriority.Normal);
							break;

						default:
							Output("Not supported: " + msg.MessageType);
							break;
					}
				}
				Thread.Sleep(1);
			}

			server.Shutdown("Application exiting");

			Thread.Sleep(100); // let disconnect make it out the door
		}

		private static void Output(string str)
		{
			Console.WriteLine(str);
			File.AppendAllText("log.txt", Stopwatch.GetTimestamp() + " - SERVER - " + str + "\n");
		}
	}
}
