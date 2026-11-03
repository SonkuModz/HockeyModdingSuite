using System;

namespace VGAudio.Codecs.Atrac9;

internal static class BandExtension
{
	public static void ApplyBandExtension(Block block)
	{
		if (block.BandExtensionEnabled && block.HasExtensionData)
		{
			Channel[] channels = block.Channels;
			for (int i = 0; i < channels.Length; i++)
			{
				ApplyBandExtensionChannel(channels[i]);
			}
		}
	}

	private static void ApplyBandExtensionChannel(Channel channel)
	{
		int quantizationUnitCount = channel.Block.QuantizationUnitCount;
		int[] scaleFactors = channel.ScaleFactors;
		double[] spectra = channel.Spectra;
		double[] bexScales = channel.BexScales;
		int[] bexValues = channel.BexValues;
		GetBexBandInfo(out var bandCount, out var groupAUnit, out var groupBUnit, quantizationUnitCount);
		int num = Math.Max(groupBUnit, 22);
		int num2 = Tables.QuantUnitToCoeffIndex[quantizationUnitCount];
		int num3 = Tables.QuantUnitToCoeffIndex[groupAUnit];
		int num4 = Tables.QuantUnitToCoeffIndex[groupBUnit];
		int num5 = Tables.QuantUnitToCoeffIndex[num];
		FillHighFrequencies(spectra, num2, num3, num4, num5);
		switch (channel.BexMode)
		{
		case 0:
		{
			int num10 = num - quantizationUnitCount;
			switch (bandCount)
			{
			case 3:
				bexScales[0] = Tables.BexMode0Bands3[0][bexValues[0]];
				bexScales[1] = Tables.BexMode0Bands3[1][bexValues[0]];
				bexScales[2] = Tables.BexMode0Bands3[2][bexValues[1]];
				bexScales[3] = Tables.BexMode0Bands3[3][bexValues[2]];
				bexScales[4] = Tables.BexMode0Bands3[4][bexValues[3]];
				break;
			case 4:
				bexScales[0] = Tables.BexMode0Bands4[0][bexValues[0]];
				bexScales[1] = Tables.BexMode0Bands4[1][bexValues[0]];
				bexScales[2] = Tables.BexMode0Bands4[2][bexValues[1]];
				bexScales[3] = Tables.BexMode0Bands4[3][bexValues[2]];
				bexScales[4] = Tables.BexMode0Bands4[4][bexValues[3]];
				break;
			case 5:
				bexScales[0] = Tables.BexMode0Bands5[0][bexValues[0]];
				bexScales[1] = Tables.BexMode0Bands5[1][bexValues[1]];
				bexScales[2] = Tables.BexMode0Bands5[2][bexValues[1]];
				break;
			}
			bexScales[num10 - 1] = Tables.SpectrumScale[scaleFactors[quantizationUnitCount]];
			AddNoiseToSpectrum(channel, Tables.QuantUnitToCoeffIndex[num - 1], Tables.QuantUnitToCoeffCount[num - 1]);
			ScaleBexQuantUnits(spectra, bexScales, quantizationUnitCount, num);
			break;
		}
		case 1:
		{
			for (int m = quantizationUnitCount; m < num; m++)
			{
				bexScales[m - quantizationUnitCount] = Tables.SpectrumScale[scaleFactors[m]];
			}
			AddNoiseToSpectrum(channel, num2, num5 - num2);
			ScaleBexQuantUnits(spectra, bexScales, quantizationUnitCount, num);
			break;
		}
		case 2:
		{
			double num13 = Tables.BexMode2Scale[bexValues[0]];
			double num14 = Tables.BexMode2Scale[bexValues[1]];
			for (int n = num2; n < num3; n++)
			{
				spectra[n] *= num13;
			}
			for (int num15 = num3; num15 < num4; num15++)
			{
				spectra[num15] *= num14;
			}
			break;
		}
		case 3:
		{
			double num11 = Math.Pow(2.0, Tables.BexMode3Rate[bexValues[1]]);
			double num12 = Tables.BexMode3Initial[bexValues[0]];
			for (int l = num2; l < num5; l++)
			{
				num12 *= num11;
				spectra[l] *= num12;
			}
			break;
		}
		case 4:
		{
			double num6 = Tables.BexMode4Multiplier[bexValues[0]];
			double num7 = 0.7079468 * num6;
			double num8 = 0.5011902 * num6;
			double num9 = 0.3548279 * num6;
			for (int i = num2; i < num3; i++)
			{
				spectra[i] *= num7;
			}
			for (int j = num3; j < num4; j++)
			{
				spectra[j] *= num8;
			}
			for (int k = num4; k < num5; k++)
			{
				spectra[k] *= num9;
			}
			break;
		}
		}
	}

	private static void ScaleBexQuantUnits(double[] spectra, double[] scales, int startUnit, int totalUnits)
	{
		for (int i = startUnit; i < totalUnits; i++)
		{
			for (int j = Tables.QuantUnitToCoeffIndex[i]; j < Tables.QuantUnitToCoeffIndex[i + 1]; j++)
			{
				spectra[j] *= scales[i - startUnit];
			}
		}
	}

	private static void FillHighFrequencies(double[] spectra, int groupABin, int groupBBin, int groupCBin, int totalBins)
	{
		for (int i = 0; i < groupBBin - groupABin; i++)
		{
			spectra[groupABin + i] = spectra[groupABin - i - 1];
		}
		for (int j = 0; j < groupCBin - groupBBin; j++)
		{
			spectra[groupBBin + j] = spectra[groupBBin - j - 1];
		}
		for (int k = 0; k < totalBins - groupCBin; k++)
		{
			spectra[groupCBin + k] = spectra[groupCBin - k - 1];
		}
	}

	private static void AddNoiseToSpectrum(Channel channel, int index, int count)
	{
		if (channel.Rng == null)
		{
			int[] scaleFactors = channel.ScaleFactors;
			ushort seed = (ushort)(543 * (scaleFactors[8] + scaleFactors[12] + scaleFactors[15] + 1));
			channel.Rng = new Atrac9Rng(seed);
		}
		for (int i = 0; i < count; i++)
		{
			channel.Spectra[i + index] = (double)(int)channel.Rng.Next() / 65535.0 * 2.0 - 1.0;
		}
	}

	public static void GetBexBandInfo(out int bandCount, out int groupAUnit, out int groupBUnit, int quantUnits)
	{
		groupAUnit = Tables.BexGroupInfo[quantUnits - 13][0];
		groupBUnit = Tables.BexGroupInfo[quantUnits - 13][1];
		bandCount = Tables.BexGroupInfo[quantUnits - 13][2];
	}
}
