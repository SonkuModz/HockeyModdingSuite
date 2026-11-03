using System;
using System.IO;
using System.Text;

namespace VGAudio.Utilities;

public class BinaryWriterBE : BinaryWriter
{
	private readonly byte[] _buffer = new byte[8];

	public BinaryWriterBE(Stream input)
		: base(input)
	{
	}

	public BinaryWriterBE(Stream output, Encoding encoding, bool leaveOpen)
		: base(output, encoding, leaveOpen)
	{
	}

	public override void Write(short value)
	{
		base.Write(Byte.ByteSwap(value));
	}

	public override void Write(ushort value)
	{
		base.Write(Byte.ByteSwap(value));
	}

	public override void Write(int value)
	{
		base.Write(Byte.ByteSwap(value));
	}

	public override void Write(uint value)
	{
		base.Write(Byte.ByteSwap(value));
	}

	public override void Write(long value)
	{
		base.Write(Byte.ByteSwap(value));
	}

	public override void Write(ulong value)
	{
		base.Write(Byte.ByteSwap(value));
	}

	public override void Write(float value)
	{
		byte[] bytes = BitConverter.GetBytes(value);
		_buffer[0] = bytes[3];
		_buffer[1] = bytes[2];
		_buffer[2] = bytes[1];
		_buffer[3] = bytes[0];
		OutStream.Write(_buffer, 0, 4);
	}

	public override void Write(double value)
	{
		Write(BitConverter.DoubleToInt64Bits(value));
	}
}
