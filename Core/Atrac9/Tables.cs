using System;
using VGAudio.Utilities;

namespace VGAudio.Codecs.Atrac9;

internal static class Tables
{
	public static readonly int[] SampleRates;

	public static readonly byte[] SamplingRateIndexToFrameSamplesPower;

	public static readonly byte[] MaxBandCount;

	public static readonly byte[] BandToQuantUnitCount;

	public static readonly byte[] QuantUnitToCoeffCount;

	public static readonly short[] QuantUnitToCoeffIndex;

	public static readonly byte[] QuantUnitToCodebookIndex;

	public static readonly ChannelConfig[] ChannelConfig;

	public static readonly HuffmanCodebook[] HuffmanScaleFactorsUnsigned;

	public static readonly HuffmanCodebook[] HuffmanScaleFactorsSigned;

	public static readonly HuffmanCodebook[][] HuffmanSpectrumA;

	public static readonly HuffmanCodebook[][] HuffmanSpectrumB;

	public static readonly HuffmanCodebook[][][] HuffmanSpectrum;

	public static readonly double[][] MdctWindow;

	public static readonly double[][] ImdctWindow;

	public static readonly double[] SpectrumScale;

	public static readonly double[] QuantizerStepSize;

	public static readonly double[] QuantizerInverseStepSize;

	public static readonly double[] QuantizerFineStepSize;

	public static readonly byte[][] GradientCurves;

	public static readonly byte[][] ScaleFactorWeights;

	public static readonly double[][] BexMode0Bands3;

	public static readonly double[][] BexMode0Bands4;

	public static readonly double[][] BexMode0Bands5;

	public static readonly double[] BexMode2Scale;

	public static readonly double[] BexMode3Initial;

	public static readonly double[] BexMode3Rate;

	public static readonly double[] BexMode4Multiplier;

	public static readonly byte[][] BexGroupInfo;

	public static readonly byte[][] BexEncodedValueCounts;

	public static readonly byte[][][] BexDataLengths;

	static Tables()
	{
		SampleRates = new int[16]
		{
			11025, 12000, 16000, 22050, 24000, 32000, 44100, 48000, 44100, 48000,
			64000, 88200, 96000, 128000, 176400, 192000
		};
		SamplingRateIndexToFrameSamplesPower = new byte[16]
		{
			6, 6, 7, 7, 7, 8, 8, 8, 6, 6,
			7, 7, 7, 8, 8, 8
		};
		MaxBandCount = new byte[16]
		{
			8, 8, 12, 12, 12, 18, 18, 18, 8, 8,
			12, 12, 12, 16, 16, 16
		};
		BandToQuantUnitCount = new byte[19]
		{
			0, 4, 8, 10, 12, 13, 14, 15, 16, 18,
			20, 21, 22, 23, 24, 25, 26, 28, 30
		};
		QuantUnitToCoeffCount = new byte[30]
		{
			2, 2, 2, 2, 2, 2, 2, 2, 4, 4,
			4, 4, 8, 8, 8, 8, 8, 8, 8, 8,
			16, 16, 16, 16, 16, 16, 16, 16, 16, 16
		};
		QuantUnitToCoeffIndex = new short[31]
		{
			0, 2, 4, 6, 8, 10, 12, 14, 16, 20,
			24, 28, 32, 40, 48, 56, 64, 72, 80, 88,
			96, 112, 128, 144, 160, 176, 192, 208, 224, 240,
			256
		};
		QuantUnitToCodebookIndex = new byte[30]
		{
			0, 0, 0, 0, 0, 0, 0, 0, 1, 1,
			1, 1, 2, 2, 2, 2, 2, 2, 2, 2,
			3, 3, 3, 3, 3, 3, 3, 3, 3, 3
		};
		ChannelConfig = new ChannelConfig[6]
		{
			new ChannelConfig(default(BlockType)),
			new ChannelConfig(default(BlockType), default(BlockType)),
			new ChannelConfig(BlockType.Stereo),
			new ChannelConfig(BlockType.Stereo, BlockType.Mono, BlockType.LFE, BlockType.Stereo),
			new ChannelConfig(BlockType.Stereo, BlockType.Mono, BlockType.LFE, BlockType.Stereo, BlockType.Stereo),
			new ChannelConfig(BlockType.Stereo, BlockType.Stereo)
		};
		MdctWindow = new double[3][]
		{
			GenerateMdctWindow(6),
			GenerateMdctWindow(7),
			GenerateMdctWindow(8)
		};
		ImdctWindow = new double[3][]
		{
			GenerateImdctWindow(6),
			GenerateImdctWindow(7),
			GenerateImdctWindow(8)
		};
		SpectrumScale = Arrays.Generate(32, SpectrumScaleFunction);
		QuantizerStepSize = Arrays.Generate(16, QuantizerStepSizeFunction);
		QuantizerInverseStepSize = Arrays.Generate(16, QuantizerInverseStepSizeFunction);
		QuantizerFineStepSize = Arrays.Generate(16, QuantizerFineStepSizeFunction);
		GradientCurves = BitAllocation.GenerateGradientCurves();
		Array[] array = ArrayUnpacker.UnpackArrays(PackedTables.Tables);
		HuffmanSpectrumA = PackedTables.GenerateHuffmanCodebooks((short[][][])array[4], (byte[][][])array[0], (byte[][])array[6]);
		HuffmanSpectrumB = PackedTables.GenerateHuffmanCodebooks((short[][][])array[5], (byte[][][])array[1], (byte[][])array[7]);
		HuffmanSpectrum = new HuffmanCodebook[2][][] { HuffmanSpectrumA, HuffmanSpectrumB };
		HuffmanScaleFactorsUnsigned = PackedTables.GenerateHuffmanCodebooks((short[][])array[9], (byte[][])array[2], (byte[])array[8]);
		HuffmanScaleFactorsSigned = PackedTables.GenerateHuffmanCodebooks((short[][])array[10], (byte[][])array[3], (byte[])array[8]);
		ScaleFactorWeights = (byte[][])array[11];
		BexGroupInfo = (byte[][])array[12];
		BexEncodedValueCounts = (byte[][])array[13];
		BexDataLengths = (byte[][][])array[14];
		BexMode0Bands3 = (double[][])array[15];
		BexMode0Bands4 = (double[][])array[16];
		BexMode0Bands5 = (double[][])array[17];
		BexMode2Scale = (double[])array[18];
		BexMode3Initial = (double[])array[19];
		BexMode3Rate = (double[])array[20];
		BexMode4Multiplier = (double[])array[21];
	}

	public static int MaxHuffPrecision(bool highSampleRate)
	{
		if (!highSampleRate)
		{
			return 7;
		}
		return 1;
	}

	public static int MinBandCount(bool highSampleRate)
	{
		if (!highSampleRate)
		{
			return 3;
		}
		return 1;
	}

	public static int MaxExtensionBand(bool highSampleRate)
	{
		if (!highSampleRate)
		{
			return 18;
		}
		return 16;
	}

	private static double QuantizerStepSizeFunction(int x)
	{
		return 2.0 / (double)((1 << x + 1) - 1);
	}

	private static double QuantizerInverseStepSizeFunction(int x)
	{
		return 1.0 / QuantizerStepSizeFunction(x);
	}

	private static double QuantizerFineStepSizeFunction(int x)
	{
		return QuantizerStepSizeFunction(x) / 65535.0;
	}

	private static double SpectrumScaleFunction(int x)
	{
		return Math.Pow(2.0, x - 15);
	}

	private static double[] GenerateImdctWindow(int frameSizePower)
	{
		int num = 1 << frameSizePower;
		double[] array = new double[num];
		double[] array2 = GenerateMdctWindow(frameSizePower);
		for (int i = 0; i < num; i++)
		{
			array[i] = array2[i] / (array2[num - 1 - i] * array2[num - 1 - i] + array2[i] * array2[i]);
		}
		return array;
	}

	private static double[] GenerateMdctWindow(int frameSizePower)
	{
		int num = 1 << frameSizePower;
		double[] array = new double[num];
		for (int i = 0; i < num; i++)
		{
			array[i] = (Math.Sin((((double)i + 0.5) / (double)num - 0.5) * Math.PI) + 1.0) * 0.5;
		}
		return array;
	}
}
