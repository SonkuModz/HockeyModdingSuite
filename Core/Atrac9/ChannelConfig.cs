namespace VGAudio.Codecs.Atrac9;

public class ChannelConfig
{
	public int BlockCount { get; }

	public BlockType[] BlockTypes { get; }

	public int ChannelCount { get; }

	public ChannelConfig(params BlockType[] blockTypes)
	{
		BlockCount = blockTypes.Length;
		BlockTypes = blockTypes;
		foreach (BlockType blockType in blockTypes)
		{
			ChannelCount += Block.BlockTypeToChannelCount(blockType);
		}
	}
}
