using System;

namespace Lidgren.Network
{
	/// <summary>
	/// Priority of an outgoing message. High priority messages are sent before Normal priority and Normal priority are sent before Low priority.
	/// </summary>
	public enum NetMessagePriority
	{
		Delayed = 0,
		Low = 1,
		Normal = 2,
		High = 3
	}
}
