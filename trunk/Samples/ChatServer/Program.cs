using System;
using System.Collections.Generic;
using System.Windows.Forms;

using Lidgren.Network;
using SamplesCommon;

namespace ChatServer
{
	static class Program
	{
		private static NetServer s_server;
		private static NetBuffer s_readBuffer;
		private static Form1 s_mainForm;
		private static double s_nextStatisticsDisplay;

		[STAThread]
		static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			s_mainForm = new Form1();

			NetConfiguration config = new NetConfiguration("chat");
			config.MaxConnections = 128;
			config.Port = 14242;
			s_server = new NetServer(config);
			s_server.Start();

			s_readBuffer = s_server.CreateBuffer();

			Application.Idle += new EventHandler(OnAppIdle);
			Application.Run(s_mainForm);

			s_server.Shutdown("Application exiting");
		}

		static void OnAppIdle(object sender, EventArgs e)
		{
			while (NativeMethods.AppStillIdle)
			{
				//s_server.Heartbeat();

				NetConnection source;
				NetMessageType type;
				while (s_server.ReadMessage(s_readBuffer, out type, out source))
				{
					switch (type)
					{
						case NetMessageType.DebugMessage:
							WriteToConsole(s_readBuffer.ReadString());
							break;
						case NetMessageType.StatusChanged:
							WriteToConsole("New status for " + source + ": " + source.Status + " (" + s_readBuffer.ReadString() + ")");
							break;
						case NetMessageType.Data:
							// handle message
							string name = s_readBuffer.ReadString();
							string text = s_readBuffer.ReadString();
							WriteToConsole(name + " wrote: " + text);

							// send to everyone (including sender)
							NetBuffer sendBuffer = s_server.CreateBuffer();
							sendBuffer.Write(name);
							sendBuffer.Write(text);
							s_server.SendToAll(sendBuffer, NetChannel.ReliableUnordered);
							break;
						default:
							WriteToConsole("Unhandled: " + type + " from " + source);
							break;
					}
				}
				
				double now = NetTime.Now;
				if (now > s_nextStatisticsDisplay)
				{
					s_mainForm.label1.Text = s_server.GetStatisticsString(s_server.Connections.Count > 0 ? s_server.Connections[0] : null);
					s_nextStatisticsDisplay = now + (1.0 / 5.0); // 5 fps
				}
				
				System.Threading.Thread.Sleep(1);
			}
		}

		private static void WriteToConsole(string text)
		{
			try
			{
				Console.WriteLine(text);
				s_mainForm.richTextBox1.AppendText(text + Environment.NewLine);
				NativeMethods.ScrollRichTextBox(s_mainForm.richTextBox1);
			}
			catch
			{
				// Gulp
			}
		}
	}
}