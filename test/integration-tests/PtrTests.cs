namespace Surab;

public class PtrTests
{
	[Fact]
	public void Ptr_Basic()
	{
		IntegrationRunner.TestAnnotatedOutput($$"""
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
				print_i32(*n_ptr); [|42|]
				print_i32(**n_ptr_ptr); [|42|]
				print_i32(*&n); [|42|] // *& cancel each other
				print_i32(**&n_ptr); [|42|]

				var s = S {
					a = 43,
				};
				var s_ptr = &s;
				var s_ptr_ptr = &s_ptr;
				print_i32((*s_ptr).a); [|43|] // explicit deref
				print_i32(s_ptr.a); [|43|] // auto deref
				print_i32(s_ptr_ptr.a); [|43|] // auto deref
				f(&s); [|43|]
				f(s_ptr); [|43|]
				f2(&s_ptr); [|43|] [|43|]

				s.mutate(); // auto address, mutation will be observed
				print_i32(s.a); [|44|]
				// Works through the ptr too.
				s_ptr.mutate();
				print_i32(s.a); [|45|]

				var s_copy1 = s; // copy
				var s_copy2 = *s_ptr; // copy
				s_copy1.mutate();
				s_copy2.mutate();
				s_copy2.mutate();
				print_i32(s_copy1.a); [|46|]
				print_i32(s_copy2.a); [|47|]
				print_i32(s.a); [|45|]

				var a_ptr = &s.a;
				print_i32(*a_ptr); [|45|]
			}
			""");
	}

	[Fact]
	public void Ptr_NoUnexpectedMutation()
	{
		IntegrationRunner.TestAnnotatedOutput($$"""
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
				f(s); [|43|] // Will be copied, so the change in f shouldn't be observed in the next line.
				print_f64(s.b); [|42|]
				f(S { a = 45.0, b = 46.0 }); [|43|]
			}
			""");
	}

	[Fact]
	public void Ptr_Arith()
	{
		IntegrationRunner.TestAnnotatedOutput($$"""
			struct S {
				a: f64;
				b: f64;
			}

			fn main() {
				var a = 2;
				var ptr = &a;
				var ptr_2 = std.memory::ptr_add_offset<i32>(ptr, 1);
				print_i32(a); [|2|]
			}
			""");
	}
}
