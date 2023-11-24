namespace Surab;

public class SmokeTests
{
    [Fact]
    public void Succeed()
    {
        var output = IntegrationRunner.Run("""
            fn main() {
                var a = 42;
                print_n(a);
            }
            """);

        var expectedOutput = "42" + Environment.NewLine;

        output.Should().Be(expectedOutput);
    }

    [Fact]
    public void Fail()
    {
        IntegrationRunner.Run("""
            fn main() {
                fffffffffffff
            }
            """, fails: true);
    }
}
