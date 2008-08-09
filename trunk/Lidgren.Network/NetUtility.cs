/* Copyright (c) 2008 Michael Lidgren

Permission is hereby granted, free of charge, to any person obtaining a copy of this software
and associated documentation files (the "Software"), to deal in the Software without
restriction, including without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom
the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or
substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE
USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Lidgren.Network
{
	/// <summary>
	/// Utility methods
	/// </summary>
	public static class NetUtility
	{
		private static Regex s_regIP;

		/// <summary>
		/// Get IP address from notation (xxx.xxx.xxx.xxx) or hostname
		/// </summary>
		public static IPAddress Resolve(string ipOrHost)
		{
			if (string.IsNullOrEmpty(ipOrHost))
				throw new ArgumentException("Supplied string must not be empty", "ipOrHost");

			ipOrHost = ipOrHost.Trim();

			if (s_regIP == null)
			{
				string expression = "\\b(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\b";
				RegexOptions options = RegexOptions.Compiled;
				s_regIP = new Regex(expression, options);
			}

			// is it an ip number string?
			IPAddress ipAddress = null;
			if (s_regIP.Match(ipOrHost).Success && IPAddress.TryParse(ipOrHost, out ipAddress))
				return ipAddress;

			// ok must be a host name
			IPHostEntry entry;
			try
			{
				entry = Dns.GetHostEntry(ipOrHost);
				if (entry == null)
					return null;

				// check each entry for a valid IP address
				foreach (IPAddress ipCurrent in entry.AddressList)
				{
					string sIP = ipCurrent.ToString();
					bool isIP = s_regIP.Match(sIP).Success && IPAddress.TryParse(sIP, out ipAddress);
					if (isIP)
						break;
				}
				if (ipAddress == null)
					return null;

				return ipAddress;
			}
			catch (SocketException ex)
			{
				if (ex.SocketErrorCode == SocketError.HostNotFound)
				{
					//LogWrite(string.Format(CultureInfo.InvariantCulture, "Failed to resolve host '{0}'.", ipOrHost));
					return null;
				}
				else
				{
					throw;
				}
			}
		}

		/// <summary>
		/// returns how many bits are necessary to hold a certain number
		/// </summary>
		[CLSCompliant(false)]
		public static int BitsToHoldUInt(uint value)
		{
			int bits = 1;
			while ((value >>= 1) != 0)
				bits++;
			return bits;
		}

		[CLSCompliant(false)]
		public static UInt32 SwapByteOrder(UInt32 value)
		{
			return
				((value & 0xff000000) >> 24) |
				((value & 0x00ff0000) >> 8) |
				((value & 0x0000ff00) << 8) |
				((value & 0x000000ff) << 24);
		}
		
		[CLSCompliant(false)]
		public static UInt64 SwapByteOrder(UInt64 value)
		{
			return
				((value & 0xff00000000000000L) >> 56) |
				((value & 0x00ff000000000000L) >> 40) |
				((value & 0x0000ff0000000000L) >> 24) |
				((value & 0x000000ff00000000L) >> 8) |
				((value & 0x00000000ff000000L) << 8) |
				((value & 0x0000000000ff0000L) << 24) |
				((value & 0x000000000000ff00L) << 40) |
				((value & 0x00000000000000ffL) << 56);
		}
	}
}
