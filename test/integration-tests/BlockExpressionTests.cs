namespace Surab;

public class BlockExpressionTests
{
    [Theory]
    [InlineData("2")]
    [InlineData("{ 2 }")]
    [InlineData("{ { 4; } 2 }")]
    [InlineData("{ { 4; }; 2 }")]
    [InlineData("{ let a = { 2 }; a }")]
    public void Test_1(string snippet)
    {
        var output = IntegrationRunner.Run($$"""
            fn main() {
                let a = {{snippet}};
                print(a);
            }
            """);

        var expectedOutput = "2" + Environment.NewLine;

        output.Should().Be(expectedOutput);
    }

    [Theory]
    [InlineData("{ { 2 } }")]
    [InlineData("{ { 4 }; 2 }")]
    public void Test_Fail_1(string snippet)
    {
        IntegrationRunner.Run($$"""
            fn main() {
                let a = {{snippet}};
                print(a);
            }
            """, fails: true);
    }

    [Fact]
    public void Test_2()
    {
        var output = IntegrationRunner.Run("""
            fn main() {
                let a = 2;
                print(a);
            }
            """);

        var expectedOutput = "2" + Environment.NewLine;

        output.Should().Be(expectedOutput);
    }

    [Fact]
    public void Test_3()
    {
        var output = IntegrationRunner.Run("""
            fn main() {
                { }
                {
                    let a = 4;
                }
                let a = 2;
                print(a);
                { }
            }
            """);

        var expectedOutput = "2" + Environment.NewLine;

        output.Should().Be(expectedOutput);
    }
}
