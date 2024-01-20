using System.Diagnostics;
using Surab;
using Surab.Analysis;
using Surab.Compilation;

ConsoleHelper.WriteInfo($"Building runtime lib...".NL());

var runtimeSrcFolderPath = PathHelper.GetRuntimeSrcFolderPath();
var p = new Process
{
	StartInfo =
	{
		FileName = "cargo",
		Arguments = "build",
	},
};

p.Start();
p.WaitForExit();

var src = """
	struct S {
		a: i32;
		s_nested: SNested;

		struct SNested {
			na: i32;

			fn create() SNested {
				SNested {
					na = 45,
				}
			}
		}
	}

	fn main() {
		fn create_s_nested() S.SNested {
			S.SNested.create()
		}

		var s = S {
			a = 42,
			s_nested = S.SNested {
				na = 43,
			},
		};
		print_i32(s.s_nested.na);
		print_i32(s.a);

		var another_s_nested = create_s_nested();
		print_i32(another_s_nested.na);
	}
	""";
var sourceFile = SourceFile.FromText(src);

var project = SurabProject.Create([sourceFile]);
var tree = project.GetSyntaxTree(sourceFile);
var reasoner = project.GetReasoner(sourceFile);

var diags = reasoner.GetAllDiagnostics();
if (diags.Any(x => x.Level >= DiagnosticLevel.Error))
{
	var errors = diags.Select(d => (d, v: d.Visualize(src))).ToList();
	ConsoleHelper.WriteError("ERROR COMPILING\n");
	Console.WriteLine(string.Join('\n', errors.Select(x => x.v)));
	return;
}

var exe = Compiler.Compile(project, "");
ConsoleHelper.WriteInfo($"Running compiled exe '{Path.GetFileName(exe)}'...".NL());
ConsoleHelper.WriteInfo("===".NL());

p = new Process
{
	StartInfo =
	{
		FileName = exe,
		Arguments = "",
	},
};

p.Start();
p.WaitForExit();

if (p.ExitCode != 0)
{
	ConsoleHelper.WriteWarning($"Program exited with non zero exit code ({p.ExitCode})".NL());
}
