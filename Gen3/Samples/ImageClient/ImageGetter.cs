using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Lidgren.Network;

namespace ImageClient
{
	public partial class ImageGetter : Form
	{
		public NetClient Client;
		public byte[] Buffer = new byte[990];

		public ImageGetter(string host)
		{
			InitializeComponent();

			NetPeerConfiguration config = new NetPeerConfiguration("ImageTransfer");
			Client = new NetClient(config);
			Client.Start();
			Client.Connect(host, 14242);
		}

		public void Heartbeat()
		{
			NetIncomingMessage inc;
			while ((inc = Client.ReadMessage()) != null)
			{
				switch(inc.MessageType)
				{
					case NetIncomingMessageType.Data:
						// image data, whee!
						// ineffective but simple data model
						ushort width = inc.ReadUInt16();
						ushort height = inc.ReadUInt16();
						uint segment = inc.ReadVariableUInt32();

						int totalBytes = (width * height * 3);
						int wholeSegments = totalBytes / 990;
						int segLen = 990;
						if (segment >= wholeSegments)
							segLen = totalBytes - (wholeSegments * 990); // last segment can be shorter

						Bitmap bm = pictureBox1.Image as Bitmap;
						if (bm == null)
						{
							bm = new Bitmap(width + 1, height + 1);
							pictureBox1.Image = bm;
							this.Size = new System.Drawing.Size(width + 40, height + 60);
							pictureBox1.SetBounds(12, 12, width, height);
						}
						pictureBox1.SuspendLayout();

						int pixelsAhead = (int)segment * 330;

						int y = pixelsAhead / width;
						int x = pixelsAhead - (y * width);

						for (int i = 0; i < (segLen / 3); i++)
						{
							// set pixel
							byte r = inc.ReadByte();
							byte g = inc.ReadByte();
							byte b = inc.ReadByte();
							Color col = Color.FromArgb(r, g, b);
							bm.SetPixel(x, y, col);
							x++;
							if (x >= width)
							{
								x = 0;
								y++;
							}
						}

						pictureBox1.ResumeLayout();

						break;
				}
			}
		}
	}
}
