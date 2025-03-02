namespace Surab;

public class GeneralTests
{
	[Fact]
	public void General_1()
	{
		IntegrationRunner.TestAnnotatedOutput(
			"""
			fn f(x: i32, y: i32) { print_i32(x); }
			fn main() {
				var a = 42;
				var b = 43;
				f(a, b); [|42|]
			}
			""");
	}

	[Fact]
	public void General_Neg()
	{
		IntegrationRunner.TestAnnotatedOutput(
			"""
			fn main() {
				print_i32(-42); [|-42|]
				print_i32(42 + -2); [|40|]
				print_i32(-42 + -2); [|-44|]
				print_i32(-42 + 2); [|-40|]
			}
			""");
	}

	[Fact]
	public void General_Not()
	{
		IntegrationRunner.TestAnnotatedOutput(
			"""
			fn main() {
				print_bool(!true); [|false|]
				print_bool(!false); [|true|]
			}
			""");
	}

	[Fact]
	public void General_FnType()
	{
		IntegrationRunner.TestAnnotatedOutput(
			"""
			fn foo1(number: i32) {
				print_i32(number + 2);
			}

			fn foo2(number: i32) {
				print_i32(number + 3);
			}

			fn main() {
				var number = 40;
				var f = foo1;
				f(number); [|42|]
				f = foo2;
				f(number); [|43|]
			}
			""");
	}

	[Fact]
	public void General_FnType_2()
	{
		IntegrationRunner.TestAnnotatedOutput(
			"""
			fn foo1(number: i32) {
				print_i32(number + 2);
			}

			fn foo2(number: i32, fn_ptr: fn (i32)) {
				fn_ptr(number);
			}

			fn foo3(number: i32, fn_ptr_ptr: *fn (i32)) {
				// A very weird case, but making sure it works regardless.
				(*fn_ptr_ptr)(number);
			}

			fn get_f() fn (i32) {
				foo1
			}

			fn main() {
				var number = 40;
				var f = foo1;
				foo2(number, f); [|42|]
				foo3(number, &f); [|42|]
				get_f()(number); [|42|]
			}
			""");
	}

	[Fact]
	public void General_FnType_3()
	{
		IntegrationRunner.TestAnnotatedOutput(
			"""
			fn foo1(number: i32) {
				print_i32(number + 2);
			}

			fn foo2(number: i32, fn_ptr: fn (i32)) {
				fn_ptr(number);
			}

			fn main() {
				foo2(40, foo1); [|42|]
			}
			""");
	}

	[Fact]
	public void General_2_1()
	{
		IntegrationRunner.Run(
			"""
			fn f() i32 { return 2; }

			fn main() {
				f();
			}
			""");
	}

	[Fact]
	public void General_2_2()
	{
		IntegrationRunner.Run(
			"""
			fn f() i32 { return 2; }

			fn main() {
				print_i32(f());
			}
			""");
	}

	[Fact]
	public void General_MainWithNoReturn()
	{
		IntegrationRunner.Run(
			"""
			fn main() { }
			""");
	}

	[Fact]
	public void General_MainWithReturn()
	{
		IntegrationRunner.Run(
			"""
			fn main() i32 { return 0; }
			""");
	}

	[Fact]
	public void General_VarShadowing()
	{
		IntegrationRunner.TestAnnotatedOutput(
			"""
			fn main() {
				var foo = 42;
				var a = 2;
				var a = true;
				print_bool(a); [|true|]
				var a = 4.2;
				{
					var a = 2;
					{
						print_i32(a); [|2|]
						print_i32(foo); [|42|]
					}
				}
				print_f64(a); [|4.2|]
			}
			""");
	}

	[Fact]
	public void General_LocalDecls()
	{
		IntegrationRunner.TestAnnotatedOutput(
			"""
			fn main() {
				struct S {
					a: i32;
				}

				var s = S.{ a: 42 };
				print_i32(s.a); [|42|]
			}
			""");
	}

	[Fact]
	public void General_LocalDecls_2()
	{
		IntegrationRunner.TestAnnotatedOutput(
			"""
			struct S {
				a: i32;
				s_nested: SNested;

				struct SNested {
					na: i32;

					fn create() SNested {
						SNested.{
							na: 45,
						}
					}
				}
			}

			fn main() {
				var s = S.{
					a: 42,
					s_nested: S.SNested.{
						na: 43,
					},
				};
				print_i32(s.s_nested.na); [|43|]
				print_i32(s.a); [|42|]

				var another_s_nested = S.SNested.create();
				print_i32(another_s_nested.na); [|45|]
			}
			""");
	}
}
