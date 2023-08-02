using System.Collections.Immutable;

namespace Surab.Compilation;

public class LoweringUtilsTests
{
    [Fact]
    public void Basic()
    {
        var node = new LoweredStatementList(
            null!,
            new LoweredStatement[]
            {
                new LoweredStatementList(
                    null!,
                    new LoweredStatement[] { new LoweredReturnStatement(null!, null) }),
                new LoweredReturnStatement(null!, null),
            });

        var list = LoweringUtils.EnumerateUnwrappedDescendants(node).ToList();
        list.Should().HaveCount(2);
        list[0].Should().BeOfType<LoweredReturnStatement>();
        list[1].Should().BeOfType<LoweredReturnStatement>();
    }
}
