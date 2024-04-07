using Surab.Analysis;

namespace Surab;

public class ModulesTests
{
	[Fact]
	public void Modules_1()
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
}
