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
			}

			fn main() {
				var n = 42;
				var s1 = S {
					a = 43,
				};
				var n_ptr = &n;
				var s1_ptr = &s1;

				print_i32(*n_ptr);
				print_i32((*s1_ptr).a); // explicit deref
				f(&s1);
				f(s1_ptr);

				s1.mutate(); // auto address, mutation will be observed
				print_i32(s1.a);
				// Works through the ptr too.
				s1_ptr.mutate();
				print_i32(s1.a);
			}
			""");

		var expectedOutput =
			"42".NL() +
			"43".NL() +
			"43".NL() +
			"43".NL() +
			"44".NL() +
			"45".NL();

		output.Should().Be(expectedOutput);
	}
}
