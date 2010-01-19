using System;

namespace Lidgren.Network2
{
	/// <summary>
	/// Priority of an outgoing message. High priority messages are sent before Normal priority and Normal priority are sent before Low priority.
	/// </summary>
	public enum NetMessagePriority
	{
		Low = 0,
		Normal = 1,
		High = 2
	}
}
