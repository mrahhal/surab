namespace Surab;

public class SmokeTest
{
    [Fact]
    public void Succeed()
    {
        var output = IntegrationRunner.Run("""
            fn main() {
                var a = 42;
                print(a);
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
