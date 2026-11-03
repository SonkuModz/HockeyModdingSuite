using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace VGAudio.Utilities;

public static class ArrayUnpacker
{
	private static readonly Type[] TypeLookup = new Type[11]
	{
		typeof(byte),
		typeof(sbyte),
		typeof(char),
		typeof(short),
		typeof(ushort),
		typeof(int),
		typeof(uint),
		typeof(long),
		typeof(ulong),
		typeof(float),
		typeof(double)
	};

	public static Array[] UnpackArrays(byte[] packedArrays)
	{
		packedArrays = TryDecompress(packedArrays);
		using MemoryStream input = new MemoryStream(packedArrays);
		using BinaryReader binaryReader = new BinaryReader(input);
		byte b = binaryReader.ReadByte();
		int num = binaryReader.ReadByte();
		if (b != 0 || num != 0)
		{
			throw new InvalidDataException();
		}
		int num2 = binaryReader.ReadUInt16();
		Array[] array = new Array[num2];
		for (int i = 0; i < num2; i++)
		{
			byte b2 = binaryReader.ReadByte();
			byte value = binaryReader.ReadByte();
			Type outType = TypeLookup[Helpers.GetHighNibble(value)];
			byte lowNibble = Helpers.GetLowNibble(value);
			array[b2] = UnpackArray(binaryReader, outType, lowNibble);
		}
		return array;
	}

	private static Array UnpackArray(BinaryReader reader, Type outType, int rank)
	{
		byte b = reader.ReadByte();
		if (b == byte.MaxValue)
		{
			return null;
		}
		byte highNibble = Helpers.GetHighNibble(b);
		Type storedType = TypeLookup[Helpers.GetLowNibble(b)];
		Type type = Arrays.MakeJaggedArrayType(outType, rank - 1);
		switch (highNibble)
		{
		case 0:
		{
			int num2 = reader.ReadUInt16();
			if (rank == 1)
			{
				return ReadArray(reader, storedType, type, num2);
			}
			Array array4 = Array.CreateInstance(type, new int[1] { num2 });
			for (int l = 0; l < num2; l++)
			{
				array4.SetValue(UnpackArray(reader, outType, rank - 1), new int[1] { l });
			}
			return array4;
		}
		case 1:
		{
			int[] array3 = new int[rank];
			for (int k = 0; k < array3.Length; k++)
			{
				array3[k] = reader.ReadUInt16();
			}
			return UnpackInternal(type, storedType, reader, 0, array3);
		}
		case 2:
		{
			int num = reader.ReadUInt16();
			int[] array = new int[num];
			for (int i = 0; i < num; i++)
			{
				array[i] = reader.ReadUInt16();
			}
			Array array2 = Array.CreateInstance(type, new int[1] { num });
			for (int j = 0; j < num; j++)
			{
				array2.SetValue(ReadArray(reader, storedType, outType, array[j]), new int[1] { j });
			}
			return array2;
		}
		default:
			throw new InvalidDataException();
		}
	}

	private static Array ReadArray(BinaryReader reader, Type storedType, Type outType, int length)
	{
		if (length == 65535)
		{
			return null;
		}
		int count = length * Marshal.SizeOf(storedType);
		Array array = Array.CreateInstance(storedType, new int[1] { length });
		Buffer.BlockCopy(reader.ReadBytes(count), 0, array, 0, count);
		if ((object)storedType != outType)
		{
			return CastArray(array, outType);
		}
		return array;
	}

	private static Array CastArray(Array inArray, Type outType)
	{
		Array array = Array.CreateInstance(outType, new int[1] { inArray.Length });
		for (int i = 0; i < inArray.Length; i++)
		{
			object value = Convert.ChangeType(inArray.GetValue(new int[1] { i }), outType);
			array.SetValue(value, new int[1] { i });
		}
		return array;
	}

	private static byte[] TryDecompress(byte[] data)
	{
		if (data[0] == 1)
		{
			int length = BitConverter.ToInt32(data, 1);
			data = Inflate(data, 5, length);
		}
		return data;
	}

	private static byte[] Inflate(byte[] compressed, int startIndex, int length)
	{
		byte[] array = new byte[length];
		using MemoryStream memoryStream = new MemoryStream(compressed);
		memoryStream.Position = startIndex;
		using DeflateStream deflateStream = new DeflateStream(memoryStream, CompressionMode.Decompress);
		deflateStream.ReadExactly(array, 0, length);
		return array;
	}

	private static Array UnpackInternal(Type outType, Type storedType, BinaryReader reader, int depth, int[] dimensions)
	{
		if (depth >= dimensions.Length)
		{
			return null;
		}
		if (depth == dimensions.Length - 1)
		{
			return ReadArray(reader, storedType, outType, dimensions[depth]);
		}
		Array array = Array.CreateInstance(outType, new int[1] { dimensions[depth] });
		for (int i = 0; i < dimensions[depth]; i++)
		{
			array.SetValue(UnpackInternal(outType.GetElementType(), storedType, reader, depth + 1, dimensions), new int[1] { i });
		}
		return array;
	}
}
