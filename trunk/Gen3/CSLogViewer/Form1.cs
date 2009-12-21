using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace CSLogViewer
{
	public partial class Form1 : Form
	{
		public List<string> Lines;

		public Form1()
		{
			Lines = new List<string>();
			InitializeComponent();
		}

		private void addTextFileToolStripMenuItem_Click(object sender, EventArgs e)
		{
			OpenFileDialog dlg = new OpenFileDialog();
			DialogResult res = dlg.ShowDialog();
			if (res != DialogResult.OK)
				return;

			string[] arr = File.ReadAllLines(dlg.FileName);
			Lines.AddRange(arr);

			UpdateResult();
		}

		private void UpdateResult()
		{
			richTextBox1.Clear();

			// sort lines
			Lines.Sort(new MyComparer());

			foreach (string line in Lines)
				richTextBox1.AppendText(line + "\n");
		}
	}

	public class MyComparer : IComparer<string>
	{
		public int Compare(string x, string y)
		{
			ulong xt = UInt64.Parse(x.Substring(0, x.IndexOf(' ')));
			ulong yt = UInt64.Parse(y.Substring(0, y.IndexOf(' ')));
			return (int)(xt - yt);
		}
	}
}
