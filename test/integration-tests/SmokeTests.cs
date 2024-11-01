namespace Surab;

public class SmokeTests
{
	[Fact]
	public void Succeed()
	{
		IntegrationRunner.TestAnnotatedOutput(
			"""
			fn main() {
				var a = 42;
				print_i32(a); [|42|]
			}
			""");
	}

	[Fact]
	public void Fail()
	{
		IntegrationRunner.Run(
			"""
			fn main() {
				fffffffffffff
			}
			""", fails: true);
	}
}
