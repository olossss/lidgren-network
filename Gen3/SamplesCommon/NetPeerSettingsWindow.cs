using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Lidgren.Network2;

namespace SamplesCommon
{
	public partial class NetPeerSettingsWindow : Form
	{
		public NetPeer Peer;

		public NetPeerSettingsWindow(string title, NetPeer peer)
		{
			Peer = peer;
			InitializeComponent();
			RefreshData();
			this.Text = title;
		}

		private void RefreshData()
		{
			LossTextBox.Text = ((int)(Peer.Configuration.SimulatedLoss * 1000)).ToString();
			DebugCheckBox.Checked = Peer.Configuration.IsMessageTypeEnabled(NetIncomingMessageType.DebugMessage);
			VerboseCheckBox.Checked = Peer.Configuration.IsMessageTypeEnabled(NetIncomingMessageType.VerboseDebugMessage);
			MinLatencyTextBox.Text = ((int)(Peer.Configuration.SimulatedMinimumLatency * 1000)).ToString();
			textBox3.Text = ((int)((Peer.Configuration.SimulatedMinimumLatency + Peer.Configuration.SimulatedRandomLatency) * 1000)).ToString();

			InfoLabel.Text = Peer.ConnectionsCount + " connections active\n" +
				"Sent " + Peer.Statistics.SentBytes + " bytes in " + Peer.Statistics.SentPackets + " packets\n" +
				"Received " + Peer.Statistics.ReceivedBytes + " bytes in " + Peer.Statistics.ReceivedPackets + " packets\n";
		}

		private void DebugCheckBox_CheckedChanged(object sender, EventArgs e)
		{
			Peer.Configuration.SetMessageTypeEnabled(NetIncomingMessageType.DebugMessage, DebugCheckBox.Checked);
		}

		private void VerboseCheckBox_CheckedChanged(object sender, EventArgs e)
		{
			Peer.Configuration.SetMessageTypeEnabled(NetIncomingMessageType.VerboseDebugMessage, DebugCheckBox.Checked);
		}

		private void LossTextBox_TextChanged(object sender, EventArgs e)
		{
			float ms;
			if (Single.TryParse(LossTextBox.Text, out ms))
				Peer.Configuration.SimulatedLoss = (float)((double)ms / 1000.0);
		}

		private void MinLatencyTextBox_TextChanged(object sender, EventArgs e)
		{
			float min;
			if (float.TryParse(MinLatencyTextBox.Text, out min))
				Peer.Configuration.SimulatedMinimumLatency = (float)(min / 1000.0);
			MinLatencyTextBox.Text = ((int)(Peer.Configuration.SimulatedMinimumLatency * 1000)).ToString();
		}
			
		private void textBox3_TextChanged(object sender, EventArgs e)
		{
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
		}

		private void button1_Click(object sender, EventArgs e)
		{
			RefreshData();
		}
	}
}
