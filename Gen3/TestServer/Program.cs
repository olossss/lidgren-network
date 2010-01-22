using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Lidgren.Network2;
using System.Threading;
using SamplesCommon;

namespace TestServer
{
	static class Program
	{
		public static Form1 MainForm;
		public static NetServer Server;

		[STAThread]
		static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);

			MainForm = new Form1();

			NetPeerConfiguration config = new NetPeerConfiguration("test");
			config.Port = 14242;
			Server = new NetServer(config);
			Server.Start();

			Application.Idle += new EventHandler(AppLoop);
			Application.Run(MainForm);

			Server.Shutdown("application exiting");
		}

		static void AppLoop(object sender, EventArgs e)
		{
			while (NativeMethods.AppStillIdle)
			{
				// update gui
				MainForm.label6.Text = Server.ConnectionsCount.ToString() + " connected";

				// read messages
				NetIncomingMessage msg;
				while((msg = Server.ReadMessage()) != null)
				{
					switch (msg.MessageType)
					{
						case NetIncomingMessageType.VerboseDebugMessage:
						case NetIncomingMessageType.DebugMessage:
						case NetIncomingMessageType.WarningMessage:
						case NetIncomingMessageType.ErrorMessage:
							ConsoleOut(msg.ReadString());
							break;
						case NetIncomingMessageType.StatusChanged:
							NetConnectionStatus status = (NetConnectionStatus)msg.ReadByte();
							ConsoleOut("New status: " + status + " (" + msg.ReadString() + ")");
							break;
						case NetIncomingMessageType.Data:
							ConsoleOut("Received string: " + msg.ReadString());
							break;
						default:
							ConsoleOut("Received " + msg.MessageType);
							break;
					}

					// we're done reading, so recycle message
					Server.Recycle(msg);
				}

				Thread.Sleep(1);
			}
		}

		public static void ConsoleOut(string str)
		{
			NativeMethods.AppendText(MainForm.richTextBox1, str);
		}
	}
}
