namespace Surab;

public class ControlFlowTests
{
	[Fact]
	public void If1()
	{
		IntegrationRunner.TestAnnotatedOutput(
			"""
			fn main() {
				if true {
					print_i32(42); [|42|]
				}
				if false {
					print_i32(43);
				}

				if false {
					print_i32(44);
				} else {
					print_i32(45); [|45|]
				}
			}
			""");
	}

	[Fact]
	public void If2()
	{
		IntegrationRunner.TestAnnotatedOutput(
			"""
			fn main() {
				var a = if true {
					42
				} else if false {
					43
				}  else {
					44
				};

				print_i32(a); [|42|]
			}
			""");
	}

	[Fact(Skip = "Not yet implemented (eq)")]
	public void If3()
	{
		IntegrationRunner.TestAnnotatedOutput(
			"""
			fn main() {
				fn p(a: i32) {
					if a == 1 {
						print_i32(42);
					} else if a == 2 {
						print_i32(43);
					} else {
						print_i32(44);
					}
				}

				p(1); [|42|]
				p(2); [|43|]
				p(3); [|44|]
			}
			""");
	}

	[Fact(Skip = "Not yet implemented (eq)")]
	public void If4()
	{
		IntegrationRunner.TestAnnotatedOutput(
			"""
			fn main() {
				var a = if true {
					42
				} else if false {
					43
				} else {
					44
				};

				print_i32(a); [|42|]

				fn p(a: i32) i32 {
					if a == 1 {
						42
					} else if a == 2 {
						43
					} else {
						44
					}
				}

				print_i32(p(1)); [|42|]
				print_i32(p(2)); [|43|]
				print_i32(p(3)); [|44|]
			}
			""");
	}
}
