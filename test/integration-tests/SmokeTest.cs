using System.Diagnostics;
using Surab.Analysis;

namespace Surab.Compilation;

public class SmokeTest
{
    [Fact]
    public void Test()
    {
        var src = """
            function print(v: Number): None {
              // Compiler intrinsic
            }

            function main(): None {
              let a = 42;
              print(a);
            }
            """;
        var tree = Parser.ParseModule(src);

        var model = SemanticModel.Create(tree);
        var diags = model.GetAllDiagnostics();
        var errors = diags.Where(x => x.Level >= DiagnosticLevel.Error);
        errors.Should().HaveCount(0);

        var exe = Compiler.Compile(model);

        var p = new Process
        {
            StartInfo =
            {
                FileName = exe,
                Arguments = "",
                RedirectStandardOutput = true,
            }
        };

        p.Start();
        p.WaitForExit();

        var expectedOutput = "42" + Environment.NewLine;
        var output = p.StandardOutput.ReadToEnd();

        output.Should().Be(expectedOutput);
    }
}
