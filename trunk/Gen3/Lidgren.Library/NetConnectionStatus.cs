using System;

namespace Lidgren.Network
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
