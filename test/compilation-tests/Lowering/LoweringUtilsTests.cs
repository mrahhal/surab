using System.Collections.Immutable;

namespace Surab.Compilation;

public class LoweringUtilsTests
{
	[Fact]
	public void Basic()
	{
		var node = new LoweredStmtList(
			null!,
			new LoweredStmt[]
			{
				new LoweredStmtList(
					null!,
					new LoweredStmt[] { new LoweredReturnStmt(null!, null) }),
				new LoweredReturnStmt(null!, null),
			});

		var list = LoweringUtils.EnumerateUnwrappedDescendants(node).ToList();
		list.Should().HaveCount(2);
		list[0].Should().BeOfType<LoweredReturnStmt>();
		list[1].Should().BeOfType<LoweredReturnStmt>();
	}
}
