using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace VGAudio.Utilities;

public static class Helpers
{
	private static readonly sbyte[] SignedNibbles = new sbyte[16]
	{
		0, 1, 2, 3, 4, 5, 6, 7, -8, -7,
		-6, -5, -4, -3, -2, -1
	};

	private static readonly int[] MultiplyDeBruijnBitPosition = new int[32]
	{
		0, 9, 1, 10, 13, 21, 2, 29, 11, 14,
		16, 18, 22, 25, 3, 30, 8, 12, 20, 28,
		15, 17, 24, 7, 19, 27, 23, 6, 26, 5,
		4, 31
	};

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int Clamp(int value, int min, int max)
	{
		if (value < min)
		{
			return min;
		}
		if (value > max)
		{
			return max;
		}
		return value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static double Clamp(double value, double min, double max)
	{
		if (value < min)
		{
			return min;
		}
		if (value > max)
		{
			return max;
		}
		return value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static short Clamp16(int value)
	{
		if (value > 32767)
		{
			return short.MaxValue;
		}
		if (value < -32768)
		{
			return short.MinValue;
		}
		return (short)value;
	}

	public static sbyte Clamp4(int value)
	{
		if (value > 7)
		{
			return 7;
		}
		if (value < -8)
		{
			return -8;
		}
		return (sbyte)value;
	}

	public static byte GetHighNibble(byte value)
	{
		return (byte)((value >> 4) & 0xF);
	}

	public static byte GetLowNibble(byte value)
	{
		return (byte)(value & 0xF);
	}

	public static sbyte GetHighNibbleSigned(byte value)
	{
		return SignedNibbles[(value >> 4) & 0xF];
	}

	public static sbyte GetLowNibbleSigned(byte value)
	{
		return SignedNibbles[value & 0xF];
	}

	public static byte CombineNibbles(int high, int low)
	{
		return (byte)((high << 4) | (low & 0xF));
	}

	public static int BitCount(int v)
	{
		return BitCount((uint)v);
	}

	public static int BitCount(uint v)
	{
		v -= (v >> 1) & 0x55555555;
		v = (v & 0x33333333) + ((v >> 2) & 0x33333333);
		return (int)(((v + (v >> 4)) & 0xF0F0F0F) * 16843009) >> 24;
	}

	public static int GetNextMultiple(int value, int multiple)
	{
		if (multiple <= 0)
		{
			return value;
		}
		if (value % multiple == 0)
		{
			return value;
		}
		return value + multiple - value % multiple;
	}

	public static bool LoopPointsAreAligned(int loopStart, int alignmentMultiple)
	{
		if (alignmentMultiple != 0)
		{
			return loopStart % alignmentMultiple == 0;
		}
		return true;
	}

	public static BinaryReader GetBinaryReader(Stream stream, Endianness endianness)
	{
		if (endianness != Endianness.LittleEndian)
		{
			return new BinaryReaderBE(stream, Encoding.UTF8, leaveOpen: true);
		}
		return new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
	}

	public static BinaryWriter GetBinaryWriter(Stream stream, Endianness endianness)
	{
		if (endianness != Endianness.LittleEndian)
		{
			return new BinaryWriterBE(stream, Encoding.UTF8, leaveOpen: true);
		}
		return new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
	}

	public static T CreateJaggedArray<T>(params int[] lengths)
	{
		return (T)InitializeJaggedArray(typeof(T).GetElementType(), 0, lengths);
	}

	private static object InitializeJaggedArray(Type type, int index, int[] lengths)
	{
		Array array = Array.CreateInstance(type, new int[1] { lengths[index] });
		Type elementType = type.GetElementType();
		if ((object)elementType == null)
		{
			return array;
		}
		for (int i = 0; i < lengths[index]; i++)
		{
			array.SetValue(InitializeJaggedArray(elementType, index + 1, lengths), new int[1] { i });
		}
		return array;
	}

	public static int[] GetPrimes(int maxPrime)
	{
		int num = maxPrime / 2;
		byte[] array = new byte[num];
		for (int i = 3; i * i < maxPrime; i += 2)
		{
			if (array[i >> 1] == 0)
			{
				for (int j = i * i; j < maxPrime; j += i * 2)
				{
					array[j >> 1] = 1;
				}
			}
		}
		List<int> list = new List<int> { 2 };
		for (int k = 1; k < num; k++)
		{
			if (array[k] == 0)
			{
				list.Add(k * 2 + 1);
			}
		}
		return list.ToArray();
	}

	/// <summary>
	/// Returns the floor of the base 2 logarithm of a specified number.
	/// </summary>
	/// <param name="value">The number whose logarithm is to be found.</param>
	/// <returns>The floor of the base 2 logarithm of <paramref name="value" />.</returns>
	public static int Log2(int value)
	{
		value |= value >> 1;
		value |= value >> 2;
		value |= value >> 4;
		value |= value >> 8;
		value |= value >> 16;
		return MultiplyDeBruijnBitPosition[(uint)((long)value * 130329821L) >> 27];
	}
}
