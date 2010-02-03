using System;
using System.Runtime.Serialization;

namespace Lidgren.Network
{
	[Serializable]
	public sealed class NetException : Exception
	{
		public NetException()
			: base()
		{
		}

		public NetException(string message)
			: base(message)
		{
		}

		public NetException(string message, Exception inner)
			: base(message, inner)
		{
		}

		private NetException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
		}
	}
}
