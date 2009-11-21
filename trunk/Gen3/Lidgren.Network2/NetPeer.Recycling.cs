using System;
using System.Collections.Generic;

namespace Lidgren.Network2
{
	public partial class NetPeer
	{
		private List<byte[]> m_storagePool;

		private void InitializeRecycling()
		{
			m_storagePool = new List<byte[]>();
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
				m_storagePool.Add(msg.m_data);

			// TODO: add message object to recycling pool
		}

		/// <summary>
		/// Recycles the for reuse
		/// </summary>
		private void Recycle(NetOutgoingMessage msg)
		{
			// TODO: add message to recycling pool
		}

		/// <summary>
		/// Creates an incoming message with the required capacity for releasing to the application
		/// </summary>
		private NetIncomingMessage CreateIncomingMessage(NetIncomingMessageType tp, int requiredCapacity)
		{
			byte[] storage = GetStorage(requiredCapacity);

			// TODO: get NetIncomingMessage object from recycling pool
			NetIncomingMessage retval = new NetIncomingMessage();
			retval.m_data = storage;
			retval.MessageType = tp;
			retval.SenderConnection = null;
			retval.SenderEndpoint = null;

			return retval;
		}
	}
}
