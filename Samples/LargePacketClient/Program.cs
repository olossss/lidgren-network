using System;
using System.Collections.Generic;
using System.Windows.Forms;

using Lidgren.Network;
using SamplesCommon;
using System.Net;

namespace LargePacketClient
{
	static class Program
	{
		private static Form1 m_mainForm;
		private static NetClient m_client;
		private static NetBuffer m_readBuffer;
		private static int m_nextSize;

		[STAThread]
		static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			m_mainForm = new Form1();

			NetConfiguration config = new NetConfiguration("largepacket");
			config.SendBufferSize = 128000;
			m_client = new NetClient(config);
			//m_client.SetMessageTypeEnabled(NetMessageType.VerboseDebugMessage, true);
			m_client.SetMessageTypeEnabled(NetMessageType.Receipt, true);

			m_readBuffer = m_client.CreateBuffer();

			Application.Idle += new EventHandler(OnAppIdle);
			Application.Run(m_mainForm);
		}

		static void OnAppIdle(object sender, EventArgs e)
		{
			while (NativeMethods.AppStillIdle)
			{
				NetMessageType type;
				NetConnection source;
				if (m_client.ReadMessage(m_readBuffer, out type))
				{
					switch (type)
					{
						case NetMessageType.ServerDiscovered:
							IPEndPoint ep = m_readBuffer.ReadIPEndPoint();
							m_client.Connect(ep);
							break;
						case NetMessageType.Receipt:
							NativeMethods.AppendText(m_mainForm.richTextBox1, "Got receipt for packet sized " + m_readBuffer.ReadInt32());
							if (m_client.Status == NetConnectionStatus.Connected)
							{
								m_nextSize *= 2;
								if (m_nextSize > m_client.Configuration.SendBufferSize)
								{
									// this is enough
									NativeMethods.AppendText(m_mainForm.richTextBox1, "Done");
									m_client.Disconnect("Done");
									return;
								}
								SendPacket();
							}
							break;
						case NetMessageType.VerboseDebugMessage:
						case NetMessageType.DebugMessage:
						case NetMessageType.BadMessageReceived:
							NativeMethods.AppendText(m_mainForm.richTextBox1, m_readBuffer.ReadString());
							break;
						case NetMessageType.StatusChanged:
							if (m_client.Status == NetConnectionStatus.Connected)
							{
								m_nextSize = 8;
								SendPacket();
							}
							break;
					}
				}
								
				System.Threading.Thread.Sleep(1);
			}
		}

		private static void SendPacket()
		{
			NetBuffer buf = new NetBuffer(); //  m_client.CreateBuffer();
			buf.EnsureBufferSize(m_nextSize * 8);

			int cnt = m_nextSize / 4;
			for (int i = 0; i < cnt; i++)
				buf.Write(i);

			NativeMethods.AppendText(m_mainForm.richTextBox1, "Sending " + m_nextSize + " byte packet");

			// any receipt data will do
			NetBuffer receipt = new NetBuffer(4);
			receipt.Write(m_nextSize);
			m_client.SendMessage(buf, NetChannel.ReliableInOrder4, receipt);
		}

		internal static void Start()
		{
			m_client.DiscoverLocalServers(14242);
		}
	}
}