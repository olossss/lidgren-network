using System;
using System.Collections.Generic;

namespace Lidgren.Network2
{
	public partial class NetPeer
	{
		private List<byte[]> m_storagePool;
		private Queue<NetIncomingMessage> m_incomingMessagesPool;

		private void InitializeRecycling()
		{
			m_storagePool = new List<byte[]>();
			m_incomingMessagesPool = new Queue<NetIncomingMessage>();
		}

		internal byte[] GetStorage(int requiredBytes)
		{
			if (m_storagePool.Count < 1)
				return new byte[requiredBytes];

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
			// TODO: return from recycled pool (and call Reset)
			NetOutgoingMessage retval = new NetOutgoingMessage();

			byte[] storage = GetStorage(m_configuration.DefaultOutgoingMessageCapacity);
			retval.m_data = storage;

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

			lock (m_incomingMessagesPool)
			{
				if (!m_incomingMessagesPool.Contains(msg))
					m_incomingMessagesPool.Enqueue(msg);
			}
		}

		/// <summary>
		/// Recycles the for reuse
		/// </summary>
		private void Recycle(NetOutgoingMessage msg)
		{
			// TODO: add message to recycling pool, or?
		}

		/// <summary>
		/// Creates an incoming message with the required capacity for releasing to the application
		/// </summary>
		internal NetIncomingMessage CreateIncomingMessage(NetIncomingMessageType tp, int requiredCapacity)
		{
			// TODO: get NetIncomingMessage object from recycling pool
			NetIncomingMessage retval;
			if (m_incomingMessagesPool.Count > 0)
			{
				lock (m_incomingMessagesPool)
				{
					if (m_incomingMessagesPool.Count > 0)
					{
						retval = m_incomingMessagesPool.Dequeue();
						retval.Reset();
					}
					else
					{
						retval = new NetIncomingMessage();
					}
				}
			}
			else
			{
				retval = new NetIncomingMessage();
			}

			retval.m_messageType = tp;
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

		internal NetIncomingMessage CreateIncomingMessage(NetIncomingMessageType tp, byte[] payload, int payloadByteLength)
		{
			// TODO: get NetIncomingMessage object from recycling pool
			NetIncomingMessage retval = new NetIncomingMessage();
			retval.m_data = payload;
			retval.m_bitLength = payloadByteLength * 8;
			retval.m_messageType = tp;
			retval.m_senderConnection = null;
			retval.m_senderEndPoint = null;

			return retval;
		}

	}
}
