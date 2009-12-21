using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lidgren.Network2;
using System.Threading;
using System.Net;
using System.IO;
using System.Diagnostics;

namespace ChatClient
{
	class Program
	{
		static void Main(string[] args)
		{
			NetPeerConfiguration config = new NetPeerConfiguration("chatapp");
			//config.EnableMessageType(NetIncomingMessageType.VerboseDebugMessage);

			NetClient client = new NetClient(config);
			client.Initialize();

			Thread.Sleep(1000); // let server start up

			//NetOutgoingMessage um = client.CreateMessage();
			//um.Write("Kokosboll");
			//client.SendUnconnectedMessage(um, new IPEndPoint(NetUtility.Resolve("localhost"), 14242));

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
						case NetIncomingMessageType.Data:
						case NetIncomingMessageType.UnconnectedData:
							Output(msg.ReadString());
							break;

						case NetIncomingMessageType.StatusChanged:
							NetConnectionStatus status = (NetConnectionStatus)msg.ReadInt32();
							string reason = msg.ReadString();
							Output("Status: " + reason + " (" + status + ")");
							break;

						default:
							Output("Not supported: " + msg.MessageType);
							break;
					}
				}
				Thread.Sleep(1);
			}

			client.Shutdown("Application exiting");

			Thread.Sleep(100); // let disconnect make it out the door
		}

		private static void Output(string str)
		{
			Console.WriteLine(str);
			File.AppendAllText("log.txt", Stopwatch.GetTimestamp() + " - CLIENT - " + str + "\n");
		}
	}
}
