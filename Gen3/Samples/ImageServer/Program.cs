using System;
using System.Collections.Generic;
using System.Windows.Forms;

using Lidgren.Network;
using SamplesCommon;
using System.Drawing;

namespace ImageServer
{
	static class Program
	{
		public static Form1 MainForm;
		public static NetServer Server;
		public static byte[] ImageData;
		public static int ImageWidth, ImageHeight;

		[STAThread]
		static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			MainForm = new Form1();

			NetPeerConfiguration config = new NetPeerConfiguration("ImageTransfer");
			config.Port = 14242;
			Server = new NetServer(config);

			Application.Idle += new EventHandler(AppLoop);
			Application.Run(MainForm);
		}

		static void AppLoop(object sender, EventArgs e)
		{
			NetIncomingMessage inc;
			while (NativeMethods.AppStillIdle)
			{
				// do stuff
				while ((inc = Server.ReadMessage()) != null)
				{
					switch (inc.MessageType)
					{
						case NetIncomingMessageType.DebugMessage:
						case NetIncomingMessageType.VerboseDebugMessage:
						case NetIncomingMessageType.WarningMessage:
						case NetIncomingMessageType.ErrorMessage:
							NativeMethods.AppendText(MainForm.richTextBox1, inc.ReadString());
							break;
						case NetIncomingMessageType.StatusChanged:
							NetConnectionStatus status = (NetConnectionStatus)inc.ReadByte();
							if (status == NetConnectionStatus.Connected)
							{
								// buffer all packets!
								uint seg = 0;
								int ptr = 0;
								while (ptr < ImageData.Length)
								{
									int l = ImageData.Length - ptr > 990 ? 990 : ImageData.Length - ptr;
									NetOutgoingMessage om = Server.CreateMessage(l);
									om.Write((ushort)ImageWidth);
									om.Write((ushort)ImageHeight);
									om.WriteVariableUInt32(seg++);
									om.Write(ImageData, ptr, l);
									ptr += 990;
									Server.SendMessage(om, inc.SenderConnection, NetDeliveryMethod.Unreliable);
								}
							}
							break;
					}
				}
				System.Threading.Thread.Sleep(1);
			}
		}

		public static void Start(string filename)
		{
			Server.Start();

			MainForm.Text = "Server: Running";

			// get image size
			Bitmap bm = Bitmap.FromFile(filename) as Bitmap;
			ImageWidth = bm.Width;
			ImageHeight = bm.Height;

			// extract color bytes
			// very slow method, but small code size
			ImageData = new byte[3 * ImageWidth * ImageHeight];
			int ptr = 0;
			for (int y = 0; y < ImageHeight; y++)
			{
				for (int x = 0; x < ImageWidth; x++)
				{
					Color color = bm.GetPixel(x, y);
					ImageData[ptr++] = color.R;
					ImageData[ptr++] = color.G;
					ImageData[ptr++] = color.B;
				}
			}

			bm.Dispose();
		}
	}
}
