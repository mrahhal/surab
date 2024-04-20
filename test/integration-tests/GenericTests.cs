namespace Surab;

public class GenericTests
{
	[Fact]
	public void Basic()
	{
		IntegrationRunner.TestAnnotatedOutput(
			"""
			struct S<T> {
				value: T;

				get_value() T { this.value }
			}

			fn main() {
				var s1 = S<i32> { value = 42 };
				var s2 = S<f64> { value = 4.2 };

				print_i32(s1.value); [|42|]
				print_f64(s2.value); [|4.2|]
				print_i32(s1.get_value()); [|42|]
				print_f64(s2.get_value()); [|4.2|]
			}
			""");
	}

	[Fact]
	public void Fns()
	{
		IntegrationRunner.TestAnnotatedOutput(
			"""
			fn foo<T>(value: T) T { value }

			fn main() {
				var a = 42;
				var b = 4.2;

				print_i32(foo<i32>(a)); [|42|]
				print_f64(foo<f64>(b)); [|4.2|]
			}
			""");
	}

	[Fact]
	public void Aliases()
	{
		IntegrationRunner.TestAnnotatedOutput(
			"""
			struct S2<T1, T2> {
				a: T1;
				b: T2;
			}

			fn main() {
				alias S2<i32, T> as S1<T>;
				alias S1<f64> as S0;

				var s2 = S2<i32, f64> { a = 42, b = 4.2 };
				var s1 = S1<f64> { a = 43, b = 4.3 };
				var s0 = S0 { a = 44, b = 4.4 };

				s2 = S0 { a = 45, b = 4.5 };
				s1 = S0 { a = 45, b = 4.5 };
				s0 = S0 { a = 45, b = 4.5 };

				s2 = S1<f64> { a = 46, b = 4.6 };
				s1 = S1<f64> { a = 46, b = 4.6 };
				s0 = S1<f64> { a = 46, b = 4.6 };

				s2 = S2<i32, f64> { a = 47, b = 4.7 };
				s1 = S2<i32, f64> { a = 47, b = 4.7 };
				s0 = S2<i32, f64> { a = 47, b = 4.7 };

				print_i32(s2.a); [|47|]
				print_f64(s2.b); [|4.7|]
				print_i32(s1.a); [|47|]
				print_f64(s1.b); [|4.7|]
				print_i32(s0.a); [|47|]
				print_f64(s0.b); [|4.7|]
			}
			""");
	}

	[Fact]
	public void Aliases_Fns()
	{
		IntegrationRunner.TestAnnotatedOutput(
			"""
			struct S2<T1, T2> {
				a: T1;
				b: T2;
			}

			fn main() {
				alias fn(T) T as OneToOne<T>;

				var a = 42;
				var b = 4.2;

				fn fn_1_def(a: i32) i32 { a }
				var fn_1: OneToOne<i32> = fn_1_def;

				fn fn_2_def(a: f64) f64 { a }
				var fn_2: OneToOne<f64> = fn_2_def;

				print_i32(fn_1(a)); [|42|]
				print_f64(fn_2(b)); [|4.2|]
			}
			""");
	}

	[Fact(Skip = "Not implemented yet")]
	public void Inference()
	{
		IntegrationRunner.TestAnnotatedOutput(
			"""
			struct S<T> {
				value: T;
			}

			fn main() {
				fn create<T>(value: T) S<T> { S { value = value } } // Inferred

				var s1 = S<i32> { value = 42 };
				var s2 = S<f64> { value = 4.2 };
				var s3 = S { value = 43 }; // Inferred
				var s4 = create(4.3); // Inferred

				print_i32(s1.value); [|42|]
				print_f64(s2.value); [|4.2|]
				print_i32(s3.value); [|43|]
				print_i32(s4.value); [|4.3|]
			}
			""");
	}
}
