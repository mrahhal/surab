namespace Surab;

public class TraitTests
{
	[Fact]
	public void Basic()
	{
		IntegrationRunner.TestAnnotatedOutput(
			"""
			trait Addition {
				add(y: i32) i32;
			}

			trait AdditionGeneric<T> {
				add_generic(y: T) T;
			}

			struct S {
				v: i32;

				impl Addition {
					add(y: i32) i32 { this.v + y }
				}

				impl AdditionGeneric<f64> {
					add_generic(y: f64) f64 { 2.0 + y }
				}
			}

			fn main() {
				var s1 = S.{ v: 1 };
				var s2 = S.{ v: 2 };
				print_i32(s1.add(1)); [|2|]
				print_i32(s2.add(1)); [|3|]
				print_f64(s2.add_generic(1.2)); [|3.2|]
			}
			""");
	}

	[Fact(Skip = "for now")]
	public void This()
	{
		IntegrationRunner.TestAnnotatedOutput(
			"""
			trait Addition {
				add(y: This) This;
			}

			struct S {
				v: i32;

				impl Addition {
					add(y: S) S { S.{ v: this.v + y.v } }
				}
			}

			fn main() {
				var s1 = S.{ v: 1 };
				var s2 = S.{ v: 2 };
				print_i32(s1.add(s2).v); [|3|]
				print_i32(s2.add(s1).v); [|3|]
			}
			""");
	}

	[Fact(Skip = "for now")]
	public void Static()
	{
		IntegrationRunner.TestAnnotatedOutput(
			"""
			trait Addition {
				fn add_static(x: This, y: This) This;
			}

			struct S {
				v: i32;

				impl Addition {
					fn add_static(x: S, y: S) S { S.{ v: x.v + y.v } }
				}
			}

			fn main() {
				var s1 = S.{ v: 1 };
				var s2 = S.{ v: 2 };
				print_i32(S.add_static(s1, s2).v); [|3|]
				print_i32(S.add_static(s2, s1).v); [|3|]
			}
			""");
	}

	// generic

	// const

	// requires other trait
}
