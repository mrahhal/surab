namespace Surab;

public class StructTests
{
	[Fact]
	public void General_StructBasics()
	{
		var output = IntegrationRunner.Run($$"""
			struct S {
				a: i32;

				get_a() i32 { this.a }
			}

			fn main() {
				var s = S.{
					a = 42,
				};
				print_i32(s.get_a());
			}
			""");

		var expectedOutput =
			"42".NL();

		output.Should().Be(expectedOutput);
	}

	[Fact]
	public void DefaultFieldValues()
	{
		var output = IntegrationRunner.Run($$"""
			struct S1 { a: i32 = 42; }

			fn main() {
				var s1 = S1.{ };
				print_i32(s1.a);
			}
			""");

		var expectedOutput = "42".NL();

		output.Should().Be(expectedOutput);
	}

	[Fact]
	public void General_TypeCreation()
	{
		var output = IntegrationRunner.Run($$"""
			struct S {
				a: i32;
				b: bool;

				c: i32 = -1;

				(c: i32 = 3) {
					this.c = c;
				}
			}

			fn main() {
				var s1 = S.{
					a = 2,
					b = false,
				};
				print_i32(s1.a);
				print_bool(s1.b);
				print_i32(s1.c);

				var s2 = S.{
					a = 2,
					b = false,
					c = 4,
				};
				print_i32(s2.c);
			}
			""");

		var expectedOutput =
			"2".NL() +
			"false".NL() +
			"3".NL() +
			"4".NL();

		output.Should().Be(expectedOutput);
	}
}
