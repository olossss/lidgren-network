using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Lidgren.Network2;

namespace TestClient
{
	public partial class Form1 : Form
	{
		public Form1()
		{
			InitializeComponent();
		}

		private void button1_Click(object sender, EventArgs e)
		{
			Program.Client.Connect("localhost", 14242);
		}

		private void button2_Click(object sender, EventArgs e)
		{
			NetClient client = Program.Client;

			string str = "Hello from client " + NetUtility.GetMacAddress().GetHashCode().ToString();
			NetOutgoingMessage msg = client.CreateMessage();
			msg.Write(str);

			client.SendMessage(msg, NetMessageChannel.ReliableUnordered, NetMessagePriority.Normal);
		}

		private void button3_Click(object sender, EventArgs e)
		{
			Program.Client.Disconnect("bye");
		}

		private void checkBox1_CheckedChanged(object sender, EventArgs e)
		{
			Program.Client.Configuration.SetMessageTypeEnabled(
				NetIncomingMessageType.VerboseDebugMessage,
				!Program.Client.Configuration.IsMessageTypeEnabled(NetIncomingMessageType.VerboseDebugMessage)
			);
		}

		private void textBox2_TextChanged(object sender, EventArgs e)
		{
			float min;
			if (float.TryParse(textBox2.Text, out min))
				Program.Client.Configuration.SimulatedMinimumLatency = (float)(min / 1000.0);
			textBox2.Text = ((int)(Program.Client.Configuration.SimulatedMinimumLatency * 1000)).ToString();
		}

		private void textBox4_TextChanged(object sender, EventArgs e)
		{
			float max;
			if (float.TryParse(textBox4.Text, out max))
			{
				max = (float)((double)max / 1000.0);
				float r = max - Program.Client.Configuration.SimulatedMinimumLatency;
				if (r > 0)
				{
					Program.Client.Configuration.SimulatedRandomLatency = r;
					double nm = (double)Program.Client.Configuration.SimulatedMinimumLatency + (double)Program.Client.Configuration.SimulatedRandomLatency;
					textBox4.Text = ((int)(max * 1000)).ToString();
				}
			}
		}
	}
}
