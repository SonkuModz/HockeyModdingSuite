using System;
using System.IO;
using System.Text;

namespace VGAudio.Utilities;

public class BinaryReaderBE : BinaryReader
{
	private readonly byte[] _bufferIn = new byte[8];

	private readonly byte[] _bufferOut = new byte[8];

	public BinaryReaderBE(Stream input)
		: base(input)
	{
	}

	public BinaryReaderBE(Stream input, Encoding encoding, bool leaveOpen)
		: base(input, encoding, leaveOpen)
	{
	}

	public override short ReadInt16()
	{
		return Byte.ByteSwap(base.ReadInt16());
	}

	public override ushort ReadUInt16()
	{
		return Byte.ByteSwap(base.ReadUInt16());
	}

	public override int ReadInt32()
	{
		return Byte.ByteSwap(base.ReadInt32());
	}

	public override uint ReadUInt32()
	{
		return Byte.ByteSwap(base.ReadUInt32());
	}

	public override long ReadInt64()
	{
		return Byte.ByteSwap(base.ReadInt64());
	}

	public override ulong ReadUInt64()
	{
		return Byte.ByteSwap(base.ReadUInt64());
	}

	public override float ReadSingle()
	{
		BaseStream.Read(_bufferIn, 0, 4);
		_bufferOut[0] = _bufferIn[3];
		_bufferOut[1] = _bufferIn[2];
		_bufferOut[2] = _bufferIn[1];
		_bufferOut[3] = _bufferIn[0];
		return BitConverter.ToSingle(_bufferOut, 0);
	}

	public override double ReadDouble()
	{
		return BitConverter.Int64BitsToDouble(ReadInt64());
	}
}
