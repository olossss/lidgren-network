using System;
using System.Collections.Generic;

namespace Lidgren.Network2
{
	public partial class NetPeer
	{
		internal byte[] GetStorage(int requiredBytes)
		{
			// TODO: get from recycling pool
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
			// TODO: add message to recycling pool
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
