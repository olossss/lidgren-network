using System;
using System.Threading;
using System.Windows.Forms;

using Lidgren.Network;

using SamplesCommon;
using System.Text;

namespace DurableServer
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

			NetPeerConfiguration config = new NetPeerConfiguration("durable");
			config.Port = 14242;
			config.SimulatedLoss = 0.05f;
			Server = new NetServer(config);
			Server.Start();

			Application.Idle += new EventHandler(AppLoop);
			Application.Run(MainForm);
		}

		private static void Display(string text)
		{
			NativeMethods.AppendText(MainForm.richTextBox1, text);
		}

		private static double m_lastLabelUpdate;
		private const double kLabelUpdateFrequency = 0.25;

		private static uint m_expectedReliableOrdered;
		private static int m_reliableOrderedCorrect;
		private static int m_reliableOrderedErrors;

		private static uint m_expectedSequenced;
		private static int m_sequencedCorrect;
		private static int m_sequencedErrors;

		static void AppLoop(object sender, EventArgs e)
		{
			while (NativeMethods.AppStillIdle)
			{
				NetIncomingMessage msg;
				while ((msg = Server.ReadMessage()) != null)
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
							uint nr = msg.ReadUInt32();
							switch (msg.DeliveryMethod)
							{
								case NetDeliveryMethod.ReliableOrdered:
									if (nr != m_expectedReliableOrdered)
									{
										m_reliableOrderedErrors++;
										m_expectedReliableOrdered = nr + 1;
									}
									else
									{
										m_reliableOrderedCorrect++;
										m_expectedReliableOrdered++;
									}
									break;
								case NetDeliveryMethod.UnreliableSequenced:
									if (nr < m_expectedSequenced)
										m_sequencedErrors++;
									else
										m_sequencedCorrect++;
									m_expectedSequenced = nr + 1;
									break;
							}
							break;
					}
				}
				Thread.Sleep(1);

				double now = NetTime.Now;
				if (now > m_lastLabelUpdate + kLabelUpdateFrequency)
				{
					UpdateLabel();
					m_lastLabelUpdate = now;
				}
			}
		}

		private static void UpdateLabel()
		{
			if (Server.ConnectionsCount < 1)
			{
				MainForm.label1.Text = "No connections";
			}
			else
			{
				StringBuilder bdr = new StringBuilder();
				bdr.Append(Server.Connections[0].Statistics.ToString());
				bdr.AppendLine("RECEIVED Reliable ordered: " + m_reliableOrderedCorrect + " received; " + m_reliableOrderedErrors + " errors");
				bdr.AppendLine("RECEIVED Sequenced: " + m_sequencedCorrect + " received; " + m_sequencedErrors + " errors");
				MainForm.label1.Text = bdr.ToString();
			}
		}
	}
}
