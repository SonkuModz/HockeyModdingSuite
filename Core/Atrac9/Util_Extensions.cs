using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace VGAudio.Utilities;

public static class Extensions
{
	public static byte[] ToByteArray(this short[] array, Endianness endianness = Endianness.LittleEndian)
	{
		byte[] array2 = new byte[array.Length * 2];
		if (endianness == Endianness.LittleEndian)
		{
			Buffer.BlockCopy(array, 0, array2, 0, array2.Length);
		}
		else
		{
			for (int i = 0; i < array.Length; i++)
			{
				array2[i * 2] = (byte)(array[i] >> 8);
				array2[i * 2 + 1] = (byte)array[i];
			}
		}
		return array2;
	}

	public static short[] ToShortArray(this byte[] array, Endianness endianness = Endianness.LittleEndian)
	{
		int num = array.Length.DivideByRoundUp(2);
		short[] array2 = new short[num];
		if (endianness == Endianness.LittleEndian)
		{
			Buffer.BlockCopy(array, 0, array2, 0, array.Length);
		}
		else
		{
			for (int i = 0; i < num; i++)
			{
				array2[i] = (short)((array[i * 2] << 8) | array[i * 2 + 1]);
			}
		}
		return array2;
	}

	public static string ReadUTF8(this BinaryReader reader, int size)
	{
		return Encoding.UTF8.GetString(reader.ReadBytes(size), 0, size);
	}

	public static void WriteUTF8(this BinaryWriter writer, string value)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(value);
		writer.Write(bytes);
	}

	public static string ReadUTF8Z(this BinaryReader reader)
	{
		long position = reader.BaseStream.Position;
		while (reader.BaseStream.ReadByte() - 1 > 0)
		{
		}
		int size = (int)(reader.BaseStream.Position - position - 1);
		reader.BaseStream.Position = position;
		string result = reader.ReadUTF8(size);
		reader.BaseStream.Position++;
		return result;
	}

	public static void WriteUTF8Z(this BinaryWriter writer, string value)
	{
		writer.WriteUTF8(value);
		writer.WriteUTF8("\0");
	}

	public static bool Eof(this BinaryReader reader)
	{
		return reader.BaseStream.Position >= reader.BaseStream.Length;
	}

	public static void Expect(this BinaryReader reader, params int[] expected)
	{
		long position = reader.BaseStream.Position;
		int num = reader.ReadInt32();
		if (!Enumerable.Contains(expected, num))
		{
			throw new InvalidDataException(string.Format("Expected {0}", new object[1] { (expected.Length > 1) ? "one of: " : "" }) + string.Format("{0}, but got {1} at offset 0x{2:X}", new object[3]
			{
				expected.ToDelimitedString(),
				num,
				position
			}));
		}
	}

	public static void Expect(this BinaryReader reader, params short[] expected)
	{
		long position = reader.BaseStream.Position;
		short num = reader.ReadInt16();
		if (!Enumerable.Contains(expected, num))
		{
			throw new InvalidDataException(string.Format("Expected {0}", new object[1] { (expected.Length > 1) ? "one of: " : "" }) + string.Format("{0}, but got {1} at offset 0x{2:X}", new object[3]
			{
				expected.ToDelimitedString(),
				num,
				position
			}));
		}
	}

	public static void Expect(this BinaryReader reader, params ushort[] expected)
	{
		long position = reader.BaseStream.Position;
		ushort num = reader.ReadUInt16();
		if (!Enumerable.Contains(expected, num))
		{
			throw new InvalidDataException(string.Format("Expected {0}", new object[1] { (expected.Length > 1) ? "one of: " : "" }) + string.Format("{0}, but got {1} at offset 0x{2:X}", new object[3]
			{
				expected.ToDelimitedString(),
				num,
				position
			}));
		}
	}

	public static void Expect(this BinaryReader reader, params byte[] expected)
	{
		long position = reader.BaseStream.Position;
		byte b = reader.ReadByte();
		if (!Enumerable.Contains(expected, b))
		{
			throw new InvalidDataException(string.Format("Expected {0}", new object[1] { (expected.Length > 1) ? "one of: " : "" }) + string.Format("{0}, but got {1} at offset 0x{2:X}", new object[3]
			{
				expected.ToDelimitedString(),
				b,
				position
			}));
		}
	}

	public static string ToDelimitedString<T>(this IList<T> items)
	{
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < items.Count; i++)
		{
			if (i != 0)
			{
				stringBuilder.Append(", ");
			}
			stringBuilder.Append(items[i]);
		}
		return stringBuilder.ToString();
	}

	public static int DivideByRoundUp(this int value, int divisor)
	{
		return (int)Math.Ceiling((double)value / (double)divisor);
	}

	public static int DivideBy2RoundUp(this int value)
	{
		return value / 2 + (value & 1);
	}
}
