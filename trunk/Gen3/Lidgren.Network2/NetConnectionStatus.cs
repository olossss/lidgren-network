using System;

namespace Lidgren.Network2
{
	/// <summary>
	/// Status of a connection
	/// </summary>
	public enum NetConnectionStatus
	{
		Disconnected,
		Connecting,
		Connected,
		Disconnecting,
	}
}
