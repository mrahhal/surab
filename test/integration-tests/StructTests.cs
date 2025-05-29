namespace Surab;

public class StructTests
{
	[Fact]
	public void Struct1()
	{
		IntegrationRunner.TestAnnotatedOutput(
			"""
			struct S {
				a: i32;

				fn new(a: i32) S {
					S.{ a: a }
				}

				get_a() i32 { this.a }
			}

			fn main() {
				var s = S.{
					a: 42,
				};
				print_i32(s.get_a()); [|42|]
				var s2 = S.new(43);
				print_i32(s2.get_a()); [|43|]
			}
			""");
	}

	[Fact]
	public void Struct2()
	{
		IntegrationRunner.TestAnnotatedOutput(
			"""
			struct S1 {
				a: f32;

				fn new() S1 {
					return S1.{ a: 42.2f32 };
				}
			}

			struct S2 {
				a: f64;

				fn new() S2 {
					return S2.{ a: 43.2 };
				}
			}

			fn main() {
				var s1 = S1.new();
				print_f32(s1.a); [|42.2|]
				var s2 = S2.new();
				print_f64(s2.a); [|43.2|]
			}
			""");
	}

	[Fact]
	public void Struct3()
	{
		IntegrationRunner.TestAnnotatedOutput(
			"""
			struct S {
				a: f64;
				b: f64;

				fn new() S {
					return S.{ a: 42.2, b: 43.2 };
				}
			}

			fn main() {
				var s = S.new();
				print_f64(s.a); [|42.2|]
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
