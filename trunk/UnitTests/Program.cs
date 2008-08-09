using System;
using System.Collections.Generic;
using System.Text;

using Lidgren.Network;
using System.Diagnostics;

namespace UnitTests
{
	class Program
	{
		static unsafe void Main(string[] args)
		{
			// JIT stuff
			NetBuffer msg = new NetBuffer(20);
			msg.Write((short)short.MaxValue);
			
			// Go
			double timeStart = NetTime.Now;

			msg = new NetBuffer(20);
			for (int n = 0; n < 10000; n++)
			{
				msg.Reset();

				msg.Write((short)short.MaxValue);
				msg.Write((short)short.MinValue);
				msg.Write((short)-42);

				msg.Write(421);
				msg.Write((byte)7);
				msg.Write(-42.8f);

				if (msg.LengthBytes != 15)
					throw new Exception("Bad message length");

				msg.Write("duke of earl");

				int bytesWritten;
				bytesWritten = msg.WriteVariableInt32(-1);
				bytesWritten = msg.WriteVariableInt32(5);
				bytesWritten = msg.WriteVariableInt32(-18);
				bytesWritten = msg.WriteVariableInt32(42);
				bytesWritten = msg.WriteVariableInt32(-420);

				msg.Write((uint)9991);

				// byte boundary kept until here

				msg.Write(true);
				msg.Write((uint)3, 5);
				msg.Write(8.111f);
				msg.Write("again");
				byte[] arr = new byte[] { 1, 6, 12, 24 };
				msg.Write(arr);
				msg.Write((byte)7, 7);
				msg.Write(Int32.MinValue);
				msg.Write(UInt32.MaxValue);
				msg.WriteRangedSingle(21.0f, -10, 50, 12);

				// test reduced bit signed writing
				msg.Write(15, 5);
				msg.Write(2, 5);
				msg.Write(0, 5);
				msg.Write(-1, 5);
				msg.Write(-2, 5);
				msg.Write(-15, 5);

				msg.Write(UInt64.MaxValue);
				msg.Write(Int64.MaxValue);
				msg.Write(Int64.MinValue);

				msg.Write(42);

				int numBits = msg.WriteRangedInteger(0, 10, 5);
				if (numBits != 4)
					throw new Exception("Ack WriteRangedInteger failed");

				// verify
				msg.Position = 0;

				short a = msg.ReadInt16();
				short b = msg.ReadInt16();
				short c = msg.ReadInt16();

				if (a != short.MaxValue || b != short.MinValue || c != -42)
					throw new Exception("Ack thpth short failed");

				if (msg.ReadInt32() != 421)
					throw new Exception("Ack thphth 1");
				if (msg.ReadByte() != (byte)7)
					throw new Exception("Ack thphth 2");
				if (msg.ReadSingle() != -42.8f)
					throw new Exception("Ack thphth 3");
				if (msg.ReadString() != "duke of earl")
					throw new Exception("Ack thphth 4");

				if (msg.ReadVariableInt32() != -1) throw new Exception("ReadVariableInt32 failed 1");
				if (msg.ReadVariableInt32() != 5) throw new Exception("ReadVariableInt32 failed 2");
				if (msg.ReadVariableInt32() != -18) throw new Exception("ReadVariableInt32 failed 3");
				if (msg.ReadVariableInt32() != 42) throw new Exception("ReadVariableInt32 failed 4");
				if (msg.ReadVariableInt32() != -420) throw new Exception("ReadVariableInt32 failed 5");

				if (msg.ReadUInt32() != 9991)
					throw new Exception("Ack thphth 4.5");

				if (msg.ReadBoolean() != true)
					throw new Exception("Ack thphth 5");
				if (msg.ReadUInt32(5) != (uint)3)
					throw new Exception("Ack thphth 6");
				if (msg.ReadSingle() != 8.111f)
					throw new Exception("Ack thphth 7");
				if (msg.ReadString() != "again")
					throw new Exception("Ack thphth 8");
				byte[] rrr = msg.ReadBytes(4);
				if (rrr[0] != arr[0] || rrr[1] != arr[1] || rrr[2] != arr[2] || rrr[3] != arr[3])
					throw new Exception("Ack thphth 9");
				if (msg.ReadByte(7) != 7)
					throw new Exception("Ack thphth 10");
				if (msg.ReadInt32() != Int32.MinValue)
					throw new Exception("Ack thphth 11");
				if (msg.ReadUInt32() != UInt32.MaxValue)
					throw new Exception("Ack thphth 12");

				float v = msg.ReadRangedSingle(-10, 50, 12);
				// v should be close to, but not necessarily exactly, 21.0f
				if ((float)Math.Abs(21.0f - v) > 0.1f)
					throw new Exception("Ack thphth *RangedSingle() failed");

				if (msg.ReadInt32(5) != 15)
					throw new Exception("Ack thphth ReadInt32 1");
				if (msg.ReadInt32(5) != 2)
					throw new Exception("Ack thphth ReadInt32 2");
				if (msg.ReadInt32(5) != 0)
					throw new Exception("Ack thphth ReadInt32 3");
				if (msg.ReadInt32(5) != -1)
					throw new Exception("Ack thphth ReadInt32 4");
				if (msg.ReadInt32(5) != -2)
					throw new Exception("Ack thphth ReadInt32 5");
				if (msg.ReadInt32(5) != -15)
					throw new Exception("Ack thphth ReadInt32 6");

				UInt64 longVal = msg.ReadUInt64();
				if (longVal != UInt64.MaxValue)
					throw new Exception("Ack thphth UInt64");
				if (msg.ReadInt64() != Int64.MaxValue)
					throw new Exception("Ack thphth Int64");
				if (msg.ReadInt64() != Int64.MinValue)
					throw new Exception("Ack thphth Int64");

				if (msg.ReadInt32() != 42)
					throw new Exception("Ack thphth end");

				if (msg.ReadRangedInteger(0, 10) != 5)
					throw new Exception("Ack thphth ranged integer");
			}

			double timeEnd = NetTime.Now;
			double timeSpan = timeEnd - timeStart;

			Console.WriteLine("All tests passed in " + (timeSpan * 1000.0) + " milliseconds");
						
			/*
			// compare tests
			int numRuns = 10000000;

			// jit
			Compare1(2);
			Compare2(2);

			// compare
			timeStart = NetTime.Now;
			for (int i = 0; i < numRuns; i++)
				Compare1(i % 42);
			timeEnd = NetTime.Now;
			double cmp1time = timeEnd - timeStart;
			
			timeStart = NetTime.Now;
			for (int i = 0; i < numRuns; i++)
				Compare2(i % 42);
			timeEnd = NetTime.Now;
			double cmp2time = timeEnd - timeStart;

			Console.WriteLine("Method 1: " + cmp1time);
			Console.WriteLine("Method 2: " + cmp2time);
			*/

			Console.ReadKey();
		}

		public static void Compare1(int val)
		{
		}

		public static void Compare2(int val)
		{
		}

	}
}
