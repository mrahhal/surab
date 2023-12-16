namespace Surab;

public class SmokeTests
{
	[Fact]
	public void Succeed()
	{
		var output = IntegrationRunner.Run("""
			fn main() {
				var a = 42;
				print_i32(a);
			}
			""");

		var expectedOutput = "42".NL();

		output.Should().Be(expectedOutput);
	}

	[Fact]
	public void Fail()
	{
		IntegrationRunner.Run("""
			fn main() {
				fffffffffffff
			}
			""", fails: true);
	}
}
