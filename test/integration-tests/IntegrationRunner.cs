using System.Diagnostics;
using Surab.Analysis;
using Surab.Compilation;

namespace Surab;

public static class IntegrationRunner
{
	/// <summary>
	/// Tests the code against an expected output.
	/// Parses the expected output from annotations (each annotation is treated as a line).
	/// </summary>
	public static void TestAnnotatedOutput(string annotatedCode)
	{
		var result = AnnotatedCodeUtil.Parse(annotatedCode, treatAnnotatedContentAsCode: false);
		var expectedOutput = string.Join(Environment.NewLine, result.Annotations.Select(a => a.Content)).NL();

		Test(result.Code, expectedOutput);
	}

	public static void Test(string code, string expectedOutput)
	{
		var output = Run(code);
		output.Should().Be(expectedOutput.ReplaceLineEndings());
	}

	public static string Run(string code, bool fails = false)
	{
		var project = SurabWorkspace.CreateInMemoryProject(code);
		return Run(project, fails);
	}

	public static string Run(SurabProject project, bool fails = false)
	{
		var totalErrors = new List<(DiagLevel, string, string)>();
		foreach (var reasoner in project.GetAllReasoners())
		{
			var diags = reasoner.GetAllDiags();
			var src = reasoner.Tree.File.Text;
			var errors = diags
				.Where(x => x.Level >= DiagLevel.Error)
				.Select(x => (x.Level, x.Message, Text: src[x.Span.File.Start..x.Span.File.End]));
			totalErrors.AddRange(errors);
		}
		if (!fails)
		{
			totalErrors.Should().HaveCount(0);
		}
		else
		{
			totalErrors.Should().NotBeEmpty();
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
