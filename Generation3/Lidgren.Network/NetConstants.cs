using System;

namespace Lidgren.Network
{
	public static class NetConstants
	{
		public const int kNetChannelsPerDeliveryMethod = 32;

		public const int kNumSequenceNumbers = ushort.MaxValue + 1; // 0 is a valid sequence number

		/// <summary>
		/// Number of channels which needs a sequence number to work
		/// </summary>
		public const int kNumSequencedChannels = ((int)NetMessageType.UserReliableOrdered + NetConstants.kNetChannelsPerDeliveryMethod) - (int)NetMessageType.UserSequenced;

		/// <summary>
		/// Number of reliable channels
		/// </summary>
		public const int kNumReliableChannels = ((int)NetMessageType.UserReliableOrdered + NetConstants.kNetChannelsPerDeliveryMethod) - (int)NetMessageType.UserReliableUnordered;
	}
}
