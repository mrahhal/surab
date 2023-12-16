namespace Surab.Compilation;

public class MemoryTests
{
	[Theory]
	[InlineData(4, 4)]
	[InlineData(8, 8)]
	[InlineData(8, 4)]
	public static void AlignForward_Aligned(int address, int alignment)
	{
		var result = Memory.AlignForward(address.AsBytes(), alignment.AsBytes());
		result.Should().Be(address.AsBytes());
	}

	[Theory]
	[InlineData(3, 4, 4)]
	[InlineData(4, 8, 8)]
	[InlineData(9, 4, 12)]
	public static void AlignForward(int address, int alignment, int expectedAddress)
	{
		var result = Memory.AlignForward(address.AsBytes(), alignment.AsBytes());
		result.Should().Be(expectedAddress.AsBytes());
	}

	[Theory]
	[InlineData(4, 4)]
	[InlineData(8, 8)]
	[InlineData(8, 4)]
	public static void AlignBackward_Aligned(int address, int alignment)
	{
		var result = Memory.AlignBackward(address.AsBytes(), alignment.AsBytes());
		result.Should().Be(address.AsBytes());
	}

	[Theory]
	[InlineData(3, 4, 0)]
	[InlineData(5, 4, 4)]
	[InlineData(10, 8, 8)]
	[InlineData(9, 4, 8)]
	public static void AlignBackward(int address, int alignment, int expectedAddress)
	{
		var result = Memory.AlignBackward(address.AsBytes(), alignment.AsBytes());
		result.Should().Be(expectedAddress.AsBytes());
	}
}
