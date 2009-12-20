using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Lidgren.Network2
{
	public partial class NetPeer
	{
		[Conditional("DEBUG")]
		internal void LogVerbose(string message)
		{
			if (m_configuration.IsMessageTypeEnabled(NetIncomingMessageType.VerboseDebugMessage))
			{
				NetIncomingMessage msg = CreateIncomingMessage(NetIncomingMessageType.VerboseDebugMessage, message.Length + (message.Length > 126 ? 2 : 1));
				msg.Write(message);
				ReleaseMessage(msg);
			}
		}

		[Conditional("DEBUG")]
		internal void LogDebug(string message)
		{
			if (m_configuration.IsMessageTypeEnabled(NetIncomingMessageType.DebugMessage))
			{
				NetIncomingMessage msg = CreateIncomingMessage(NetIncomingMessageType.DebugMessage, message.Length + (message.Length > 126 ? 2 : 1));
				msg.Write(message);
				ReleaseMessage(msg);
			}
		}

		internal void LogWarning(string message)
		{
			if (m_configuration.IsMessageTypeEnabled(NetIncomingMessageType.WarningMessage))
			{
				NetIncomingMessage msg = CreateIncomingMessage(NetIncomingMessageType.WarningMessage, message.Length + (message.Length > 126 ? 2 : 1));
				msg.Write(message);
				ReleaseMessage(msg);
			}
		}

		internal void LogError(string message)
		{
			if (m_configuration.IsMessageTypeEnabled(NetIncomingMessageType.ErrorMessage))
			{
				NetIncomingMessage msg = CreateIncomingMessage(NetIncomingMessageType.ErrorMessage, message.Length + (message.Length > 126 ? 2 : 1));
				msg.Write(message);
				ReleaseMessage(msg);
			}
		}
	}
}
