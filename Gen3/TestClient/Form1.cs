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
	}
}
