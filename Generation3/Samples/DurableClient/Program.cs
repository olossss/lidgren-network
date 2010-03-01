using System;
using System.Collections.Generic;
using System.Windows.Forms;

using System.Threading;
using Lidgren.Network;
using SamplesCommon;
using System.Text;

namespace DurableClient
{
	static class Program
	{
		public static Form1 MainForm;
		public static NetClient Client;

		private static bool m_sendStuff;

		[STAThread]
		static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			MainForm = new Form1();

			NetPeerConfiguration config = new NetPeerConfiguration("durable");
			config.SimulatedLoss = 0.05f;
			Client = new NetClient(config);
			Client.Start();

			Application.Idle += new EventHandler(AppLoop);
			Application.Run(MainForm);
		}

		public static void Display(string text)
		{
			NativeMethods.AppendText(MainForm.richTextBox1, text);
		}

		private static double m_nextSendReliableOrdered;
		private static uint m_reliableOrderedNr = 0;

		private static double m_nextSendSequenced;
		private static uint m_sequencedNr = 0;

		private static double m_lastLabelUpdate;
		private const double kLabelUpdateFrequency = 0.25;

		static void AppLoop(object sender, EventArgs e)
		{
			while (NativeMethods.AppStillIdle)
			{
				NetIncomingMessage msg;
				while ((msg = Client.ReadMessage()) != null)
				{
					switch (msg.MessageType)
					{
						case NetIncomingMessageType.VerboseDebugMessage:
						case NetIncomingMessageType.DebugMessage:
						case NetIncomingMessageType.WarningMessage:
						case NetIncomingMessageType.Error:
							Display(msg.ReadString());
							break;
						case NetIncomingMessageType.Data:
							break;
						case NetIncomingMessageType.StatusChanged:
							NetConnectionStatus status = (NetConnectionStatus)msg.ReadByte();
							if (status == NetConnectionStatus.Connected)
							{
								// go
								m_sendStuff = true;
							}
							break;
					}
				}

				if (m_sendStuff)
				{
					double now = NetTime.Now;

					if (now > m_nextSendReliableOrdered)
					{
						NetOutgoingMessage om = Client.CreateMessage();
						om.Write((uint)m_reliableOrderedNr);
						m_reliableOrderedNr++;
						Client.SendMessage(om, NetDeliveryMethod.ReliableOrdered);
						m_nextSendReliableOrdered = now + NetRandom.Instance.NextFloat() + 0.1f;
					}

					if (now > m_nextSendSequenced)
					{
						NetOutgoingMessage om = Client.CreateMessage();
						om.Write((uint)m_sequencedNr);
						m_sequencedNr++;
						Client.SendMessage(om, NetDeliveryMethod.UnreliableSequenced);
						m_nextSendSequenced = now + NetRandom.Instance.NextFloat() + 0.1f;
					}

					if (now > m_lastLabelUpdate + kLabelUpdateFrequency)
					{
						UpdateLabel();
						m_lastLabelUpdate = now;
					}
				}

				Thread.Sleep(1);
			}
		}

		private static void UpdateLabel()
		{
			NetConnection conn = Client.ServerConnection;
			if (conn != null)
			{
				StringBuilder bdr = new StringBuilder();
				bdr.Append(Client.Statistics.ToString());
				bdr.Append(conn.Statistics.ToString());

				bdr.AppendLine("SENT Reliable ordered: " + m_reliableOrderedNr);
				bdr.AppendLine("SENT Sequenced: " + m_sequencedNr);
				MainForm.label1.Text = bdr.ToString();
			}
		}
	}
}
