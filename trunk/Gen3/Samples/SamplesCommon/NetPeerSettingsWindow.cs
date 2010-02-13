using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Reflection;

using Lidgren.Network;

namespace SamplesCommon
{
	public partial class NetPeerSettingsWindow : Form
	{
		public NetPeer Peer;
		public Timer timer;

		public NetPeerSettingsWindow(string title, NetPeer peer)
		{
			Peer = peer;
			InitializeComponent();
			RefreshData();
			this.Text = title;

			// auto refresh now and then
			timer = new Timer();
			timer.Interval = 250;
			timer.Tick += new EventHandler(timer_Tick);
			timer.Enabled = true;
		}

		protected override void OnClosed(EventArgs e)
		{
			timer.Enabled = false;
			base.OnClosed(e);
		}

		void timer_Tick(object sender, EventArgs e)
		{
			RefreshData();
		}

		private void RefreshData()
		{
#if DEBUG
			LossTextBox.Text = ((int)(Peer.Configuration.SimulatedLoss * 100)).ToString();
			textBox2.Text = ((int)(Peer.Configuration.SimulatedDuplicatesChance * 100)).ToString();
			MinLatencyTextBox.Text = ((int)(Peer.Configuration.SimulatedMinimumLatency * 1000)).ToString();
			textBox3.Text = ((int)((Peer.Configuration.SimulatedMinimumLatency + Peer.Configuration.SimulatedRandomLatency) * 1000)).ToString();
#else
			LossTextBox.Text = "0";
			MinLatencyTextBox.Text = "0";
			textBox3.Text = "0";
			textBox2.Text = "0";
#endif
			DebugCheckBox.Checked = Peer.Configuration.IsMessageTypeEnabled(NetIncomingMessageType.DebugMessage);
			VerboseCheckBox.Checked = Peer.Configuration.IsMessageTypeEnabled(NetIncomingMessageType.VerboseDebugMessage);
			textBox1.Text = (Peer.Configuration.KeepAliveDelay * 1000).ToString();
			StatisticsLabel.Text = Peer.Statistics.ToString();

			if (Peer.ConnectionsCount > 0)
			{
				NetConnection conn = Peer.Connections[0];
			}
		}

		private void DebugCheckBox_CheckedChanged(object sender, EventArgs e)
		{
			Peer.Configuration.SetMessageTypeEnabled(NetIncomingMessageType.DebugMessage, DebugCheckBox.Checked);
		}

		private void VerboseCheckBox_CheckedChanged(object sender, EventArgs e)
		{
			Peer.Configuration.SetMessageTypeEnabled(NetIncomingMessageType.VerboseDebugMessage, VerboseCheckBox.Checked);
		}

		private void LossTextBox_TextChanged(object sender, EventArgs e)
		{
#if DEBUG
			float ms;
			if (Single.TryParse(LossTextBox.Text, out ms))
				Peer.Configuration.SimulatedLoss = (float)((double)ms / 100.0);
#endif
		}

		private void textBox2_TextChanged(object sender, EventArgs e)
		{
#if DEBUG
			float ms;
			if (Single.TryParse(textBox2.Text, out ms))
				Peer.Configuration.SimulatedDuplicatesChance = (float)((double)ms / 100.0);
#endif
		}

		private void MinLatencyTextBox_TextChanged(object sender, EventArgs e)
		{
#if DEBUG
			float min;
			if (float.TryParse(MinLatencyTextBox.Text, out min))
				Peer.Configuration.SimulatedMinimumLatency = (float)(min / 1000.0);
			MinLatencyTextBox.Text = ((int)(Peer.Configuration.SimulatedMinimumLatency * 1000)).ToString();
#endif
		}
			
		private void textBox3_TextChanged(object sender, EventArgs e)
		{
#if DEBUG
			float max;
			if (float.TryParse(textBox3.Text, out max))
			{
				max = (float)((double)max / 1000.0);
				float r = max - Peer.Configuration.SimulatedMinimumLatency;
				if (r > 0)
				{
					Peer.Configuration.SimulatedRandomLatency = r;
					double nm = (double)Peer.Configuration.SimulatedMinimumLatency + (double)Peer.Configuration.SimulatedRandomLatency;
					textBox3.Text = ((int)(max * 1000)).ToString();
				}
			}
#endif
		}

		private void button1_Click(object sender, EventArgs e)
		{
			RefreshData();
		}

		private void textBox1_TextChanged(object sender, EventArgs e)
		{
			float d;
			if (float.TryParse(textBox1.Text, out d))
				Peer.Configuration.KeepAliveDelay = (float)(d / 1000.0);
		}

		private void button2_Click(object sender, EventArgs e)
		{
			this.Close();
		}

	}
}
