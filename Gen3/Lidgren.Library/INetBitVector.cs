using System;

namespace Lidgren.Network
{
	public interface INetBitVector
	{
		void Clear();
		void Clear(int index);
		bool IsSet(int index);
		void Set(int index);
		void Set(int index, bool value);
		void ShiftRight(int steps);
		void ShiftLeft(int steps);

		bool this[int index] { get; set; }

		uint First32Bits { get; }
	}
}
