using Surab.Analysis;

namespace Surab;

public class ModulesTests
{
	[Fact]
	public void Basic()
	{
		var vfs = VFS.Create();

		vfs.AddCodeFile("/math/add.surab", $$"""
			@public
			fn add(x: i32, y: i32) i32 {
				x + y
			}
			""");
		vfs.AddCodeFile("/math/sub.surab", $$"""
			@public
			fn sub(x: i32, y: i32) i32 {
				x - y
			}
			""");

		vfs.AddCodeFile("/calc.surab", $$"""
			import math;

			fn calc_and_print() {
				print_i32(math::add(4, 2));
				print_i32(math::sub(4, 2));
			}
			""");
		vfs.AddCodeFile("/main.surab", $$"""
			fn main() {
				calc_and_print();
			}
			""");

		var project = SurabProject.Create(vfs);
		var output = IntegrationRunner.Run(project);

		var expectedOutput =
			"6".NL() +
			"2".NL();

		output.Should().Be(expectedOutput);
	}

	[Fact]
	public void Structs()
	{
		var vfs = VFS.Create();

		vfs.AddCodeFile("/mod1/foo.surab", $$"""
			@public
			struct Foo {
				a: i32;
			}
			""");
		vfs.AddCodeFile("/mod1/bar.surab", $$"""
			@public
			struct Bar {
				a: i32;
			}
			""");

		vfs.AddCodeFile("/baz.surab", $$"""
			@public
			struct Baz {
				a: i32;
			}
			""");
		vfs.AddCodeFile("/main.surab", $$"""
			import mod1;

			fn main() {
				var foo = mod1::Foo { a = 42 };
				var bar = mod1::Bar { a = 43 };
				var baz = Baz { a = 44 };

				print_i32(foo.a);
				print_i32(bar.a);
				print_i32(baz.a);
			}
			""");

		var project = SurabProject.Create(vfs);
		var output = IntegrationRunner.Run(project);

		var expectedOutput =
			"42".NL() +
			"43".NL() +
			"44".NL();

		output.Should().Be(expectedOutput);
	}

	[Fact]
	public void NestedModules()
	{
		var vfs = VFS.Create();

		vfs.AddCodeFile("/mod1/mod2/add.surab", $$"""
			@public
			fn add(x: i32, y: i32) i32 {
				x + y
			}
			""");

		vfs.AddCodeFile("/main.surab", $$"""
			import mod1.mod2;

			fn main() {
				print_i32(mod2::add(4, 2));
			}
			""");

		var project = SurabProject.Create(vfs);
		var output = IntegrationRunner.Run(project);

		var expectedOutput =
			"6".NL();

		output.Should().Be(expectedOutput);
	}
}
