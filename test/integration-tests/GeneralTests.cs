namespace Surab;

public class GeneralTests
{
    [Fact]
    public void General_1()
    {
        var output = IntegrationRunner.Run($$"""
            fn f(x: Number, y: Number) { print_n(x); }
            fn main() {
                var a = 42;
                var b = 43;
                f(a, b);
            }
            """);

        var expectedOutput = "42" + Environment.NewLine;

        output.Should().Be(expectedOutput);
    }
}
