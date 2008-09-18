using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SamplesCommon;
using Lidgren.Network;

namespace LargePacketServer
{
	static class Program
	{
		private static NetServer m_server;
		private static NetBuffer m_readBuffer;
		private static Form1 m_mainForm;
 
		[STAThread]
		static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			m_mainForm = new Form1();

			NetConfiguration config = new NetConfiguration("largepacket");
			config.Port = 14242;
			config.MaxConnections = 16;
			m_server = new NetServer(config);
			//m_server.SetMessageTypeEnabled(NetMessageType.VerboseDebugMessage, true);
			m_server.Start();

			m_readBuffer = m_server.CreateBuffer();

			Application.Idle += new EventHandler(OnAppIdle);
			Application.Run(m_mainForm);

			m_server.Shutdown("Application exiting");
		}

		static void OnAppIdle(object sender, EventArgs e)
		{
			while (NativeMethods.AppStillIdle)
			{
				NetMessageType type;
				NetConnection source;
				if (m_server.ReadMessage(m_readBuffer, out type, out source))
				{
					switch (type)
					{
						case NetMessageType.VerboseDebugMessage:
						case NetMessageType.DebugMessage:
						case NetMessageType.BadMessageReceived:
						case NetMessageType.StatusChanged:
							NativeMethods.AppendText(m_mainForm.richTextBox1, m_readBuffer.ReadString());
							break;
						case NetMessageType.Data:
							int cnt = m_readBuffer.LengthBytes / 4;
							if (cnt * 4 != m_readBuffer.LengthBytes)
								throw new NetException("Bad size!");

							for (int i = 0; i < cnt; i++)
							{
								int a = m_readBuffer.ReadInt32();
								if (a != i)
									throw new NetException("Bad data!");
							}

							NativeMethods.AppendText(m_mainForm.richTextBox1, "Verified " + m_readBuffer.LengthBytes + " bytes in a single message");
							
							break;
					}
				}

				System.Threading.Thread.Sleep(1);
			}
		}
	}
}