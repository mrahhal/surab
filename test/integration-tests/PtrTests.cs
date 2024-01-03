namespace Surab;

public class PtrTests
{
	[Fact]
	public void Ptr_Basic()
	{
		var output = IntegrationRunner.Run($$"""
			struct S {
				a: i32;

				mutate() {
					this.a = this.a + 1;
				}
			}

			fn f(s: *S) {
				print_i32(s.a); // auto deref
				var s_copy = *s;
			}

			fn f2(s: **S) {
				print_i32(s.a); // auto deref
				var s_copy = **s; // copy
				print_i32(s_copy.a); // auto address
			}

			fn main() {
				var n = 42;
				var n_ptr = &n;
				var n_ptr_ptr = &n_ptr;
				print_i32(*n_ptr);
				print_i32(**n_ptr_ptr);
				print_i32(*&n); // *& cancel each other
				print_i32(**&n_ptr);

				var s = S {
					a = 43,
				};
				var s_ptr = &s;
				var s_ptr_ptr = &s_ptr;
				print_i32((*s_ptr).a); // explicit deref
				print_i32(s_ptr.a); // auto deref
				print_i32(s_ptr_ptr.a); // auto deref
				f(&s);
				f(s_ptr);
				f2(&s_ptr);

				s.mutate(); // auto address, mutation will be observed
				print_i32(s.a);
				// Works through the ptr too.
				s_ptr.mutate();
				print_i32(s.a);

				var s_copy1 = s; // copy
				var s_copy2 = *s_ptr; // copy
				s_copy1.mutate();
				s_copy2.mutate();
				s_copy2.mutate();
				print_i32(s_copy1.a);
				print_i32(s_copy2.a);
				print_i32(s.a);

				var a_ptr = &s.a;
				print_i32(*a_ptr);
			}
			""");

		var expectedOutput =
			"42".NL() +
			"42".NL() +
			"42".NL() +
			"42".NL() +

			"43".NL() +
			"43".NL() +
			"43".NL() +
			"43".NL() +
			"43".NL() +
			"43".NL() +
			"43".NL() +

			"44".NL() +
			"45".NL() +

			"46".NL() +
			"47".NL() +
			"45".NL() +

			"45".NL();

		output.Should().Be(expectedOutput);
	}

	[Fact]
	public void Ptr_NoUnexpectedMutation()
	{
		var output = IntegrationRunner.Run($$"""
			struct S {
				a: f64;
				b: f64;
			}

			@extern
			fn f(s: S) {
				s.b = 43.0;
				print_f64(s.b);
			}

			fn main() {
				var s = S { a = 41.0, b = 42.0 };
				f(s); // Will be copied, so the change in f shouldn't be observed in the next line.
				print_f64(s.b);
				f(S { a = 45.0, b = 46.0 });
			}
			""");

		var expectedOutput =
			"43".NL() +
			"42".NL() +
			"43".NL();

		output.Should().Be(expectedOutput);
	}
}
