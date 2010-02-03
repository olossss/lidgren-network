using System;

namespace Lidgren.Network
{
	/// <summary>
	/// Type of incoming message
	/// </summary>
	public enum NetIncomingMessageType
	{
		StatusChanged,			// Data (string)
		UnconnectedData,		// Data					Based on data received
		ConnectionApproval,		// Data
		Data,					// Data					Based on data received
		Receipt,				// Data
		VerboseDebugMessage,	// Data (string)
		DebugMessage,			// Data (string)
		WarningMessage,			// Data (string)
		ErrorMessage,			// Data (string)
	}
}
