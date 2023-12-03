using System.Diagnostics;
using Surab.Analysis;
using Surab.Compilation;

namespace Surab;

public static class IntegrationRunner
{
    public static string Run(string src, bool fails = false)
    {
        var tree = Parser.ParseModule(src);

        var model = SemanticModel.Create(tree);
        var diags = model.GetAllDiagnostics();
        var errors = diags.Where(x => x.Level >= DiagnosticLevel.Error);
        if (!fails)
        {
            errors.Should().HaveCount(0);
        }
        else
        {
            errors.Should().NotBeEmpty();
            return string.Empty;
        }

        var exe = Compiler.Compile(model, "");

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

        if (!fails)
        {
            p.ExitCode.Should().Be(0);
        }
        else
        {
            p.ExitCode.Should().NotBe(0);
        }

        var output = p.StandardOutput.ReadToEnd().ReplaceLineEndings();
        return output;
    }
}
