namespace VGAudio.Utilities;

public static class Byte
{
	public static short ByteSwap(short value)
	{
		return (short)ByteSwap((ushort)value);
	}

	public static int ByteSwap(int value)
	{
		return (int)ByteSwap((uint)value);
	}

	public static long ByteSwap(long value)
	{
		return (long)ByteSwap((ulong)value);
	}

	public static ushort ByteSwap(ushort value)
	{
		return (ushort)((value >> 8) | (value << 8));
	}

	public static uint ByteSwap(uint value)
	{
		value = (value >> 16) | (value << 16);
		return ((value & 0xFF00FF00u) >> 8) | ((value & 0xFF00FF) << 8);
	}

	public static ulong ByteSwap(ulong value)
	{
		value = (value >> 32) | (value << 32);
		value = ((value & 0xFFFF0000FFFF0000uL) >> 16) | ((value & 0xFFFF0000FFFFL) << 16);
		return ((value & 0xFF00FF00FF00FF00uL) >> 8) | ((value & 0xFF00FF00FF00FFL) << 8);
	}
}
