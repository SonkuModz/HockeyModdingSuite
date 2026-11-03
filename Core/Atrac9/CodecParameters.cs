namespace VGAudio.Codecs;

public class CodecParameters
{
	public IProgressReport Progress { get; set; }

	public int SampleCount { get; set; } = -1;

	public CodecParameters()
	{
	}

	protected CodecParameters(CodecParameters source)
	{
		if (source != null)
		{
			Progress = source.Progress;
			SampleCount = source.SampleCount;
		}
	}
}
