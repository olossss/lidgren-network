using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Lidgren.Network;
using SamplesCommon;
using System.Net;

namespace ChatClient
{
	static class Program
	{
		private static Form1 s_mainForm;
		private static NetClient s_client;
		private static double s_nextStatisticsDisplay;
		private static NetBuffer s_readBuffer;

		[STAThread]
		static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			s_mainForm = new Form1();

			System.Threading.Thread.Sleep(150); // wait to let server start up

			NetConfiguration config = new NetConfiguration("chat");
			s_client = new NetClient(config);
			s_client.DiscoverLocalServers(14242);

			s_readBuffer = s_client.CreateBuffer();

			Application.Idle += new EventHandler(OnAppIdle);
			Application.Run(s_mainForm);

			s_client.Shutdown("Application exiting");
		}

		static void OnAppIdle(object sender, EventArgs e)
		{
			while (NativeMethods.AppStillIdle)
			{
				NetMessageType type;
				while (s_client.ReadMessage(s_readBuffer, out type))
				{
					switch (type)
					{
						case NetMessageType.DebugMessage:
							WriteToConsole(s_readBuffer.ReadString());
							break;
						case NetMessageType.ServerDiscovered:
							//
							// just connect to first server we find
							//

							// hail data; checked by OnConnectionRequest
							byte[] hail = new byte[2];
							hail[0] = 42;
							hail[1] = 43;

							// read server address and connect to it
							s_client.Connect(s_readBuffer.ReadIPEndPoint(), hail);
							break;

						case NetMessageType.StatusChanged:
							WriteToConsole("New status: " + s_client.Status + " (" + s_readBuffer.ReadString() + ")");
							break;

						case NetMessageType.Data:
							// handle message
							string name = s_readBuffer.ReadString();
							string text = s_readBuffer.ReadString();
							WriteToConsole(name + " wrote: " + text);
							break;

						default:
							WriteToConsole("Unhandled: " + type);
							break;
					}
				}

				double now = NetTime.Now;
				if (now > s_nextStatisticsDisplay)
				{
					s_mainForm.label1.Text = s_client.GetStatisticsString(s_client.ServerConnection);
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

		internal static void Input(string str)
		{
			// send message
			NetBuffer buffer = s_client.CreateBuffer();

			// write name and message
			buffer.Write(Environment.MachineName);
			buffer.Write(str);

			s_client.SendMessage(buffer, NetChannel.ReliableUnordered);
		}
	}
}