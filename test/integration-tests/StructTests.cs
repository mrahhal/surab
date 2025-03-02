namespace Surab;

public class StructTests
{
	[Fact]
	public void General_StructBasics()
	{
		IntegrationRunner.TestAnnotatedOutput(
			"""
			struct S {
				a: i32;

				get_a() i32 { this.a }
			}

			fn main() {
				var s = S.{
					a: 42,
				};
				print_i32(s.get_a()); [|42|]
			}
			""");
	}

	[Fact]
	public void DefaultFieldValues()
	{
		IntegrationRunner.TestAnnotatedOutput(
			"""
			struct S1 { a: i32 = 42; }

			fn main() {
				var s1 = S1.{ };
				print_i32(s1.a); [|42|]
			}
			""");
	}

	[Fact]
	public void General_TypeCreation()
	{
		IntegrationRunner.TestAnnotatedOutput(
			"""
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
					a: 2,
					b: false,
				};
				print_i32(s1.a); [|2|]
				print_bool(s1.b); [|false|]
				print_i32(s1.c); [|3|]

				var s2 = S.{
					a: 2,
					b: false,
					c: 4,
				};
				print_i32(s2.c); [|4|]
			}
			""");
	}
}
