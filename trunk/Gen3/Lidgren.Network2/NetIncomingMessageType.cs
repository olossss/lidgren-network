using System;

namespace Lidgren.Network2
{
	/// <summary>
	/// Type of incoming message
	/// </summary>
	public enum NetIncomingMessageType
	{
		StatusChanged,			// Status (Int32), Message (string)
		UnconnectedData,		// Data
		ConnectionApproval,		// Data
		Data,					// Data
		Receipt,				// Data
		VerboseDebugMessage,	// Message (string)
		DebugMessage,			// Message (string)
		BadMessageReceived,		// Message (string)
		ConnectionRejected,		// Message (string)
	}
}
