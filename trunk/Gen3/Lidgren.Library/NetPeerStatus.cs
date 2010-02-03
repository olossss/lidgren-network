using System;
using System.Collections.Generic;

namespace Lidgren.Network
{
	internal enum NetPeerStatus
	{
		NotRunning = 0,
		Starting = 1,
		Running = 2,
		ShutdownRequested = 3,
	}
}
