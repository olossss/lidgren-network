using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Lidgren.Network;
using SamplesCommon;

namespace PeerToPeer
{
	static class Program
	{
		private static Form1 s_mainForm;
		private static NetPeer s_peer;
		private static NetBuffer s_readBuffer;

		[STAThread]
		static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			s_mainForm = new Form1();

			WriteToConsole("Type 'connect <host> <port>' to connect to another peer, or 'connect <port>' to connect to another localhost peer");

			NetConfiguration config = new NetConfiguration("p2pchat");
			config.MaxConnections = 256;
			s_peer = new NetPeer(config);
			//s_peer.VerboseLog = true;

			s_peer.SetMessageTypeEnabled(NetMessageType.ConnectionRejected | NetMessageType.BadMessageReceived | NetMessageType.VerboseDebugMessage, true);

			// start listening for incoming connections
			s_peer.Start();

			// create a buffer to read data into
			s_readBuffer = s_peer.CreateBuffer();

			WriteToConsole("Listening on port " + s_peer.ListenPort);

			Application.Idle += new EventHandler(OnAppIdle);
			Application.Run(s_mainForm);

			s_peer.Shutdown("Application exiting");
		}

		static void OnAppIdle(object sender, EventArgs e)
		{
			while (NativeMethods.AppStillIdle)
			{
				NetMessageType type;
				NetConnection source;
				while (s_peer.ReadMessage(s_readBuffer, out type, out source))
					HandleMessage(type, source, s_readBuffer);

				System.Threading.Thread.Sleep(1);
			}
		}

		private static void HandleMessage(NetMessageType type, NetConnection source, NetBuffer buffer)
		{
			switch (type)
			{
				case NetMessageType.DebugMessage:
				case NetMessageType.VerboseDebugMessage:
				case NetMessageType.BadMessageReceived:
				case NetMessageType.ConnectionRejected:
					WriteToConsole(buffer.ReadString());
					break;
				case NetMessageType.Data:
					WriteToConsole(source.RemoteEndpoint + " writes: " + buffer.ReadString());
					break;
				case NetMessageType.ServerDiscovered:
					// discovered another peer!
					s_peer.Connect(buffer.ReadIPEndPoint());
					break;
				default:
					// unhandled
					break;
			}
		}

		private static void WriteToConsole(string text)
		{
			try
			{
				s_mainForm.richTextBox1.AppendText(text + Environment.NewLine);
				NativeMethods.ScrollRichTextBox(s_mainForm.richTextBox1);
			}
			catch { }
		}

		static void OnStatusChanged(object sender, NetStatusChangedEventArgs e)
		{
			WriteToConsole(e.Connection.RemoteEndpoint + ": " + e.NewStatus + " (" + e.Reason + ")");
		}

		internal static void Input(string str)
		{
			if (str.StartsWith("discover "))
			{
				s_peer.DiscoverLocalPeers(Int32.Parse(str.Substring(9)));
				return;
			}

			if (str.StartsWith("connect "))
			{
				int idx = str.IndexOf(' ', 8);
				if (idx == -1)
				{
					// port only
					s_peer.Connect("localhost", Int32.Parse(str.Substring(8)), null);
					return;
				}
				else
				{
					// host and port
					string host = str.Substring(8, idx - 8);
					string portStr = str.Substring(idx + 1);
					s_peer.Connect(host, Int32.Parse(portStr), null);
					return;
				}
			}

			NetBuffer buffer = s_peer.CreateBuffer();
			buffer.Write(str);

			// send to all other peers
			s_peer.SendToAll(buffer, NetChannel.ReliableUnordered);
		}
	}
}