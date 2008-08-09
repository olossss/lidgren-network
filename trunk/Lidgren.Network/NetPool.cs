using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace Lidgren.Network
{
	[DebuggerDisplay("Read = {m_readPtr} Write =  {m_writePtr}")]
	internal sealed class NetPool<T> where T : class, new()
	{
		private object m_lock;
		private T[] m_pool;
		private int m_writePtr;
		private int m_readPtr;

		/// <summary>
		/// Amount of objects currently in the pool
		/// </summary>
		public int Count
		{
			get
			{
				lock (m_lock)
				{
					if (m_readPtr <= m_writePtr)
						return m_writePtr - m_readPtr;
					else
						return (m_writePtr + m_pool.Length - m_readPtr);
				}
			}
		}

		internal NetPool(int maxCount, int initialCount)
		{
			Debug.Assert(initialCount <= maxCount);

			m_pool = new T[maxCount];
			m_writePtr = 0;
			m_readPtr = 0;
			for (int i = 0; i < initialCount; i++)
			{
				T item = new T();
				m_pool[m_writePtr++] = item;
			}
			if (m_writePtr >= maxCount)
				m_writePtr -= maxCount;

			m_lock = new object();
		}

		internal T Pop()
		{
			lock (m_lock)
			{
				T retval = m_pool[m_readPtr++];
				if (m_readPtr == m_pool.Length)
					m_readPtr = 0;

				if (m_readPtr == m_writePtr)
				{
					// pool is empty; allocate so it contains at least one element
					T item = new T();
					//LogVerbose("Pool item created: " + typeof(T).Name);

					m_pool[m_readPtr] = item;
					m_writePtr++;
					if (m_writePtr == m_pool.Length)
						m_writePtr = 0;
				}

				//LogVerbose("Pool item popped: " + typeof(T).Name + " Pool count: " + this.Count);
				return retval;
			}
		}

		internal void Push(T item)
		{
			lock (m_lock)
			{
#if DEBUG
				if (item.GetType() == typeof(NetMessage))
					Debug.Assert((item as NetMessage).m_data == null);
#endif
				if (m_writePtr == m_readPtr)
					return; // pool is full
				m_pool[m_writePtr++] = item;
				if (m_writePtr == m_pool.Length)
					m_writePtr = 0;

				//LogVerbose("Item pushed onto pool: " + typeof(T).Name + " Pool count: " + this.Count);
			}
		}
	}
}