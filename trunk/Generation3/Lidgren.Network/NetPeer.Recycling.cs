/* Copyright (c) 2010 Michael Lidgren

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
using System.Collections.Generic;
using System;

namespace Lidgren.Network
{
	public partial class NetPeer
	{
		private List<byte[]> m_storagePool;
		private NetQueue<NetIncomingMessage> m_incomingMessagesPool;
		private NetQueue<NetOutgoingMessage> m_outgoingMessagesPool;

		private void InitializeRecycling()
		{
			m_storagePool = new List<byte[]>();
			m_incomingMessagesPool = new NetQueue<NetIncomingMessage>(16);
			m_outgoingMessagesPool = new NetQueue<NetOutgoingMessage>(16);
		}
		
		internal byte[] GetStorage(int requiredBytes)
		{
			if (m_storagePool.Count < 1)
			{
				m_statistics.m_bytesAllocated += requiredBytes;
				return new byte[requiredBytes];
			}

			lock (m_storagePool)
			{
				int cnt = m_storagePool.Count;

				// search from end to start
				for (int i = m_storagePool.Count - 1; i >= 0; i--)
				{
					byte[] retval = m_storagePool[i];
					if (retval.Length >= requiredBytes)
					{
						m_storagePool.RemoveAt(i);
						return retval;
					}
				}
			}

			m_statistics.m_bytesAllocated += requiredBytes;
			return new byte[requiredBytes];
		}

		/// <summary>
		/// Create a new message for sending
		/// </summary>
		public NetOutgoingMessage CreateMessage()
		{
			return CreateMessage(m_configuration.DefaultOutgoingMessageCapacity);
		}

		/// <summary>
		/// Create a new message for sending
		/// </summary>
		public NetOutgoingMessage CreateMessage(int initialCapacity)
		{
			NetOutgoingMessage retval = m_outgoingMessagesPool.TryDequeue();
			if (retval == null)
				retval = new NetOutgoingMessage();
			else
				retval.Reset();

			byte[] storage = GetStorage(m_configuration.DefaultOutgoingMessageCapacity);
			retval.m_data = storage;

			return retval;
		}

		internal NetOutgoingMessage CreateLibraryMessage(NetMessageLibraryType tp, string content)
		{
			NetOutgoingMessage retval = CreateMessage(1 + (content == null ? 0 : content.Length));
			retval.m_type = NetMessageType.Library;
			retval.m_libType = tp;
			retval.Write((content == null ? "" : content));
			return retval;
		}

		/// <summary>
		/// Recycle the message to the library for reuse
		/// </summary>
		public void Recycle(NetIncomingMessage msg)
		{
			lock (m_storagePool)
			{
				if (!m_storagePool.Contains(msg.m_data))
					m_storagePool.Add(msg.m_data);
			}

			msg.m_data = null;
			m_incomingMessagesPool.Enqueue(msg);
		}

		/// <summary>
		/// Recycle the message to the library for reuse
		/// </summary>
		internal void Recycle(NetOutgoingMessage msg)
		{
#if DEBUG
			lock (m_connections)
			{
				foreach (NetConnection conn in m_connections)
				{
					if (conn.m_unsentMessages.Contains(msg))
						throw new NetException("Ouch! Recycling unsent message!");

					for(int i=0;i<conn.m_storedMessages.Length;i++)
					{
						List<NetOutgoingMessage> list = conn.m_storedMessages[i];
						if (list != null && list.Count > 0)
						{
							foreach (NetOutgoingMessage om in conn.m_storedMessages[i])
							{
								if (om == msg)
									throw new NetException("Ouch! Recycling stored message!");
							}
						}
					}
				}
			}
#endif

			lock (m_storagePool)
			{
				if (!m_storagePool.Contains(msg.m_data))
					m_storagePool.Add(msg.m_data);
			}

			msg.m_data = null;
			m_outgoingMessagesPool.Enqueue(msg);
		}

		/// <summary>
		/// Creates an incoming message with the required capacity for releasing to the application
		/// </summary>
		internal NetIncomingMessage CreateIncomingMessage(NetIncomingMessageType tp, string contents)
		{
			NetIncomingMessage retval;
			if (string.IsNullOrEmpty(contents))
			{
				retval = CreateIncomingMessage(tp, 1);
				retval.Write("");
				return retval;
			}

			byte[] bytes = System.Text.Encoding.UTF8.GetBytes(contents);
			retval = CreateIncomingMessage(tp, bytes.Length + (bytes.Length > 127 ? 2 : 1));
			retval.Write(contents);

			return retval;
		}

		/// <summary>
		/// Creates an incoming message with the required capacity for releasing to the application
		/// </summary>
		internal NetIncomingMessage CreateIncomingMessage(NetIncomingMessageType tp, int requiredCapacity)
		{
			NetIncomingMessage retval = m_incomingMessagesPool.TryDequeue();
			if (retval == null)
				retval = new NetIncomingMessage();
			else
				retval.Reset();

			retval.m_incomingType = tp;
			retval.m_senderConnection = null;
			retval.m_senderEndPoint = null;

			if (requiredCapacity > 0)
			{
				byte[] storage = GetStorage(requiredCapacity);
				retval.m_data = storage;
			}
			else
			{
				retval.m_data = null;
			}

			return retval;
		}

		internal NetIncomingMessage CreateIncomingMessage(NetIncomingMessageType tp, byte[] copyFrom, int offset, int copyLength)
		{
			NetIncomingMessage retval = m_incomingMessagesPool.TryDequeue();
			if (retval == null)
				retval = new NetIncomingMessage();
			else
				retval.Reset();

			retval.m_data = GetStorage(copyLength);
			Buffer.BlockCopy(copyFrom, offset, retval.m_data, 0, copyLength);

			retval.m_bitLength = copyLength * 8;
			retval.m_incomingType = tp;
			retval.m_senderConnection = null;
			retval.m_senderEndPoint = null;

			return retval;
		}

	}
}
