namespace Surab;

public class StructTests
{
    [Fact(Skip = "Not implemented yet")]
    public void DefaultFieldValues()
    {
        var output = IntegrationRunner.Run($$"""
            struct S1 { a = 42; }
            fn main() {
                var s1 = S1 { };
                print_n(s1.a);
            }
            """);

        var expectedOutput = "42" + Environment.NewLine;

        output.Should().Be(expectedOutput);
    }
}
