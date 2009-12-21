using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace CSLogViewer
{
	static class Program
	{
		private static Form1 m_mainForm;

		[STAThread]
		static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			m_mainForm = new Form1();

			Application.Run(m_mainForm);
		}
	}
}
