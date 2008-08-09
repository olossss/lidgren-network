using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace StressClient
{
	public partial class Form1 : Form
	{
		public Form1()
		{
			InitializeComponent();
		}

		private void button1_Click(object sender, EventArgs e)
		{
			Program.Run(textBox2.Text, Int32.Parse(textBox3.Text), Int32.Parse(textBox1.Text), Int32.Parse(textBox4.Text));
		}

		private void button2_Click(object sender, EventArgs e)
		{
			Program.Shutdown();
		}
	}
}