namespace Surab;

public class GeneralTests
{
    [Fact]
    public void General_1()
    {
        var output = IntegrationRunner.Run($$"""
            fn f(x: i32, y: i32) { print_i32(x); }
            fn main() {
                var a = 42;
                var b = 43;
                f(a, b);
            }
            """);

        var expectedOutput = "42".NL();

        output.Should().Be(expectedOutput);
    }

    [Fact]
    public void General_Neg()
    {
        var output = IntegrationRunner.Run($$"""
            fn main() {
                print_i32(-42);
                print_i32(42 + -2);
                print_i32(-42 + -2);
                print_i32(-42 + 2);
            }
            """);

        var expectedOutput =
            "-42".NL() +
            "40".NL() +
            "-44".NL() +
            "-40".NL();

        output.Should().Be(expectedOutput);
    }

    [Fact]
    public void General_Not()
    {
        var output = IntegrationRunner.Run($$"""
            fn main() {
                print_bool(!true);
                print_bool(!false);
            }
            """);

        var expectedOutput =
            "false".NL() +
            "true".NL();

        output.Should().Be(expectedOutput);
    }
}
