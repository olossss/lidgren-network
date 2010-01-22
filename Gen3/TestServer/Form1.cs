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

namespace TestServer
{
	public partial class Form1 : Form
	{
		public Form1()
		{
			InitializeComponent();
		}

		private void richTextBox1_TextChanged(object sender, EventArgs e)
		{

		}

		private void button1_Click(object sender, EventArgs e)
		{
			// send string to all
			NetOutgoingMessage msg = Program.Server.CreateMessage();
			msg.Write("Server says hello!");

			Program.Server.SendMessage(msg, Program.Server.Connections, NetMessageChannel.ReliableUnordered, NetMessagePriority.Normal);
		}

		private void button2_Click(object sender, EventArgs e)
		{
			NetServer server = Program.Server;

			foreach (NetConnection conn in server.Connections)
				conn.Disconnect("See you later alligator");
		}

		private void button3_Click(object sender, EventArgs e)
		{
			NetPeerSettingsWindow win = new NetPeerSettingsWindow("Server settings", Program.Server);
			win.Show();
		}
	}
}
