using System;

namespace Lidgren.Network
{
	public sealed class NetBitVector
	{
		private int m_capacity;
		private uint[] m_data;

		public int Capacity { get { return m_capacity; } }

		public NetBitVector(int bitsCapacity)
		{
			m_capacity = bitsCapacity;
			m_data = new uint[(bitsCapacity + 7) / 8];
		}

		public bool Get(int bitIndex)
		{
			int idx = bitIndex / 8;
			uint data = m_data[idx];
			int bitNr = bitIndex - (idx * 8);
			return (data & (1 << bitNr)) != 0;
		}

		public void Set(int bitIndex, bool value)
		{
			int idx = bitIndex / 8;
			int bitNr = bitIndex - (idx * 8);
			if (value)
				m_data[idx] |= (uint)(1 << bitNr);
			else
				m_data[idx] &= (uint)(~(1 << bitNr));
		}

		public void Clear()
		{
			m_data.Initialize();
		}
	}
}
