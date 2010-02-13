using System;
using System.Collections.Generic;
using System.Text;

namespace Lidgren.Network
{
	/// <summary>
	/// Bit vector of 32 bits
	/// </summary>
	public struct NetBitVector32 : INetBitVector
	{
		private uint m_data;

		public uint First32Bits { get { return m_data; } }

		public bool IsSet(int index)
		{
			return (m_data & ((uint)1 << index)) != 0;
		}

		public void Set(int index, bool value)
		{
			if (value)
				m_data |= ((uint)1 << index);
			else
				m_data &= ~((uint)1 << index);
		}

		public void Set(int index)
		{
			m_data |= ((uint)1 << index);
		}

		public void Clear()
		{
			m_data = 0;
		}

		public void Clear(int index)
		{
			m_data &= ~((uint)1 << index);
		}

		public bool this[int index]
		{
			get
			{
				return (m_data & ((uint)1 << index)) != 0;
			}
			set
			{
				if (value)
					m_data |= ((uint)1 << index);
				else
					m_data &= ~((uint)1 << index);
			}
		}

		public int GetValueAsInt32()
		{
			return (int)m_data;
		}

		public void ShiftRight(int steps)
		{
			m_data = m_data >> steps;
		}

		public void ShiftLeft(int steps)
		{
			m_data = m_data << steps;
		}

		/*
			int lenMinusOne = m_data.Length - 1;

			// shift full ints first
			while(steps >= 64)
			{
				for (int i = 0; i < m_data.Length - 1; i++)
					m_data[i] = m_data[i+1];
				m_data[lenMinusOne - 1] = 0;
				steps -= 64;
			}

			if (steps == 0)
				return;

			for (int i = 0; i < lenMinusOne; i++)
			{
				ulong result = m_data[i] >> steps;
				result |= (ulong)((ulong)m_data[i + 1] << (64 - steps));
				m_data[i] = result;
			}
			m_data[lenMinusOne] = m_data[lenMinusOne] >> steps;
		}
		*/

		public override string ToString()
		{
			StringBuilder bdr = new StringBuilder(32);
			for (int i = 31; i >= 0; i--)
				bdr.Append(IsSet(i) ? '1' : '0');
			return bdr.ToString();
		}
	}

	/// <summary>
	/// Bit vector of 64 bits
	/// </summary>
	public struct NetBitVector64 : INetBitVector
	{
		private ulong m_data;

		public uint First32Bits { get { return (uint)m_data; } }

		public bool IsSet(int index)
		{
			return (m_data & ((ulong)1 << index)) != 0;
		}

		public void Set(int index, bool value)
		{
			if (value)
				m_data |= ((ulong)1 << index);
			else
				m_data &= ~((ulong)1 << index);
		}

		public void Set(int index)
		{
			m_data |= ((ulong)1 << index);
		}

		public void Clear()
		{
			m_data = 0;
		}

		public void Clear(int index)
		{
			m_data &= ~((ulong)1 << index);
		}

		public bool this[int index]
		{
			get
			{
				return (m_data & ((ulong)1 << index)) != 0;
			}
			set
			{
				if (value)
					m_data |= ((ulong)1 << index);
				else
					m_data &= ~((ulong)1 << index);
			}
		}

		public int GetValueAsInt32()
		{
			return (int)m_data;
		}

		public void ShiftRight(int steps)
		{
			m_data = m_data >> steps;
		}

		public void ShiftLeft(int steps)
		{
			m_data = m_data << steps;
		}

		public override string ToString()
		{
			StringBuilder bdr = new StringBuilder(64);
			for (int i = 31; i >= 0; i--)
				bdr.Append(IsSet(i) ? '1' : '0');
			return bdr.ToString();
		}
	}
}
