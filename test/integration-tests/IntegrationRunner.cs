using System.Diagnostics;
using Surab.Analysis;
using Surab.Compilation;

namespace Surab;

public static class IntegrationRunner
{
	public static string Run(string src, bool fails = false)
	{
		var sourceFile = SourceFile.FromText(src);

		var project = SurabProject.Create([sourceFile]);
		var reasoner = project.GetReasoner(sourceFile);

		var diags = reasoner.GetAllDiagnostics();
		var errors = diags.Where(x => x.Level >= DiagnosticLevel.Error).Select(x => new { x.Level, x.Message, Text = src[x.Span.File.Start..x.Span.File.End] });
		if (!fails)
		{
			errors.Should().HaveCount(0);
		}
		else
		{
			errors.Should().NotBeEmpty();
			return string.Empty;
		}

		var exe = Compiler.Compile(project, "");

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
