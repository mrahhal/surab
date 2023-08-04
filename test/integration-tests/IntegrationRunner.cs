using System.Diagnostics;
using Surab.Analysis;
using Surab.Compilation;

namespace Surab;

public static class IntegrationRunner
{
    public static string Run(string src, bool fails = false)
    {
        src = """
            function print(v: Number) {
              // Compiler intrinsic
            }
            """ + src;

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

        var output = p.StandardOutput.ReadToEnd();
        return output;
    }
}
