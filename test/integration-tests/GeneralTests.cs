namespace Surab;

public class GeneralTests
{
	[Fact]
	public void General_1()
	{
		var output = IntegrationRunner.Run($$"""
			fn f(x: i32, y: i32) { print_i32(x); }
			fn main() {
				var a = 42;
				var b = 43;
				f(a, b);
			}
			""");

		var expectedOutput = "42".NL();

		output.Should().Be(expectedOutput);
	}

	[Fact]
	public void General_Neg()
	{
		var output = IntegrationRunner.Run($$"""
			fn main() {
				print_i32(-42);
				print_i32(42 + -2);
				print_i32(-42 + -2);
				print_i32(-42 + 2);
			}
			""");

		var expectedOutput =
			"-42".NL() +
			"40".NL() +
			"-44".NL() +
			"-40".NL();

		output.Should().Be(expectedOutput);
	}

	[Fact]
	public void General_Not()
	{
		var output = IntegrationRunner.Run($$"""
			fn main() {
				print_bool(!true);
				print_bool(!false);
			}
			""");

		var expectedOutput =
			"false".NL() +
			"true".NL();

		output.Should().Be(expectedOutput);
	}

	[Fact]
	public void General_FnType()
	{
		var output = IntegrationRunner.Run($$"""
			fn foo1(number: i32) {
				print_i32(number + 2);
			}

			fn foo2(number: i32) {
				print_i32(number + 3);
			}

			fn main() {
				var number = 40;
				var f = foo1;
				f(number);
				f = foo2;
				f(number);
			}
			""");

		var expectedOutput =
			"42".NL() +
			"43".NL();

		output.Should().Be(expectedOutput);
	}

	[Fact]
	public void General_FnType_2()
	{
		var output = IntegrationRunner.Run($$"""
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
				foo2(number, f);
				foo3(number, &f);
				get_f()(number);
			}
			""");

		var expectedOutput =
			"42".NL() +
			"42".NL() +
			"42".NL();

		output.Should().Be(expectedOutput);
	}

	[Fact]
	public void General_FnType_3()
	{
		var output = IntegrationRunner.Run($$"""
			fn foo1(number: i32) {
				print_i32(number + 2);
			}

			fn foo2(number: i32, fn_ptr: fn (i32)) {
				fn_ptr(number);
			}

			fn main() {
				foo2(40, foo1);
			}
			""");

		var expectedOutput =
			"42".NL();

		output.Should().Be(expectedOutput);
	}

	[Fact]
	public void General_2_1()
	{
		IntegrationRunner.Run($$"""
			fn f() i32 { return 2; }

			fn main() {
				f();
			}
			""");
	}

	[Fact]
	public void General_2_2()
	{
		IntegrationRunner.Run($$"""
			fn f() i32 { return 2; }

			fn main() {
				print_i32(f());
			}
			""");
	}

	[Fact]
	public void General_MainWithNoReturn()
	{
		IntegrationRunner.Run($$"""
			fn main() { }
			""");
	}

	[Fact]
	public void General_MainWithReturn()
	{
		IntegrationRunner.Run($$"""
			fn main() i32 { return 0; }
			""");
	}

	[Fact]
	public void General_VarShadowing()
	{
		var output = IntegrationRunner.Run($$"""
			fn main() {
				var foo = 42;
				var a = 2;
				var a = true;
				print_bool(a);
				var a = 4.2;
				{
					var a = 2;
					{
						print_i32(a);
						print_i32(foo);
					}
				}
				print_f64(a);
			}
			""");

		var expectedOutput =
			"true".NL() +
			"2".NL() +
			"42".NL() +
			"4.2".NL();

		output.Should().Be(expectedOutput);
	}

	[Fact]
	public void General_LocalDecls()
	{
		var output = IntegrationRunner.Run($$"""
			fn main() {
				struct S {
					a: i32;
				}

				var s = S { a = 42 };
				print_i32(s.a);
			}
			""");

		var expectedOutput =
			"42".NL();

		output.Should().Be(expectedOutput);
	}
}
