namespace Surab;

public class AliasTests
{
	[Fact]
	public void Alias_1()
	{
		var output = IntegrationRunner.Run(
			"""
			struct S1 { a: i32; }
			alias S1 as AliasOfS1;

			fn f1(s1: AliasOfS1) { print_i32(s1.a); }
			fn f2(s1: S1) { print_i32(s1.a); }

			fn main() {
				var a: AliasOfS1 = S1.{ a: 42 };
				f1(a);
				f2(a);
			}
			""");

		var expectedOutput =
			"42".NL() +
			"42".NL();

		output.Should().Be(expectedOutput);
	}

	[Fact]
	public void Alias_2()
	{
		var output = IntegrationRunner.Run(
			"""
			struct S1 { a: i32; }
			alias fn (S1) as Delegate1;

			fn f(s1: S1, d_fn: Delegate1) { d_fn(s1); }
			fn f1(s1: S1) { print_i32(s1.a); }

			fn main() {
				var a = S1.{ a: 42 };
				f(a, f1);
			}
			""");

		var expectedOutput =
			"42".NL();

		output.Should().Be(expectedOutput);
	}
}
