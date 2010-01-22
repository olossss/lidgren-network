using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Lidgren.Network2;

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
		}

		private void button2_Click(object sender, EventArgs e)
		{
			NetServer server = Program.Server;

			foreach (NetConnection conn in server.Connections)
				conn.Disconnect("See you later alligator");
		}

		private void textBox2_TextChanged_1(object sender, EventArgs e)
		{
			float min;
			if (float.TryParse(textBox2.Text, out min))
				Program.Server.Configuration.SimulatedMinimumLatency = (float)(min / 1000.0);
			textBox2.Text = ((int)(Program.Server.Configuration.SimulatedMinimumLatency * 1000)).ToString();
		}

		private void textBox4_TextChanged_1(object sender, EventArgs e)
		{
			float max;
			if (float.TryParse(textBox4.Text, out max))
			{
				max = (float)((double)max / 1000.0);
				float r = max - Program.Server.Configuration.SimulatedMinimumLatency;
				if (r > 0)
				{
					Program.Server.Configuration.SimulatedRandomLatency = r;
					double nm = (double)Program.Server.Configuration.SimulatedMinimumLatency + (double)Program.Server.Configuration.SimulatedRandomLatency;
					textBox4.Text = ((int)(max * 1000)).ToString();
				}
			}
		}
	}
}
