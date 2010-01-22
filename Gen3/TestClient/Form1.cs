using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Lidgren.Network2;
using SamplesCommon;

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
				
		private void button4_Click(object sender, EventArgs e)
		{
			NetPeerSettingsWindow win = new NetPeerSettingsWindow("Client settings", Program.Client);
			win.Show();
		}
	}
}
