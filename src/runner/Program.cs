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
		WorkingDirectory = runtimeSrcFolderPath,
	},
};

p.Start();
p.WaitForExit();

var code = """
	alias @extern fn (S2) as Delegate1;

	struct S1 {
		a: f64;
		b: bool;
	}

	@extern
	struct S2 {
		a: f64;
	}

	@extern
	struct S3 {
		a: f64;
		b: f64;

		@extern
		struct S4 {
			s3: S3;
		}
	}

	@extern
	fn print_from_rust(s2: S2);

	@extern
	fn get_s3_from_rust() S3;

	@extern
	fn print_s3_from_rust(s3: S3);

	@extern
	fn f(x: f64, y: f64) f64 {
		var e = x + y;
		return e + 1.0;
	}

	fn fs(s1: S1, s2: S2) {
		print_f64(s1.a);
		print_f64(s2.a);
	}

	fn main() {
		var a = 2.0;
		fn noop() { }
		noop();
		var b: f64 = 4.0;
		var c = f(a, b);
		c = {
			var c2 = 999.0;
			c2 + 0.2
		};
		print_f64(c);
		print_i32(41 + 1);

		var s1 = S1.{
			a: 82.0,
			b: true,
		};
		print_f64(s1.a);
		s1.a = 83.0;
		print_f64(s1.a);
		print_bool(s1.b);

		var s2 = S2.{ a: 42.3 };

		fs(s1, s2);

		print_from_rust(s2);

		// Fn pointers
		var fn_ptr /* : @extern fn (f64, f64) f64 */ = f;
		print_f64(fn_ptr(2.0, 3.0));

		var fn_ptr: @extern fn (S2) = print_from_rust;
		fn_ptr(s2);

		var fn_ptr: Delegate1 = print_from_rust;
		fn_ptr(s2);

		var s3_from_rust = get_s3_from_rust();
		print_s3_from_rust(s3_from_rust);

		var s4 = S3.S4.{ s3: s3_from_rust };
		print_s3_from_rust(s4.s3);

		var str = "This is created in surab and printed from rust.";
		print_string(str);
	}
	""";

var vfs = VFS.Create();
var codeFile = vfs.AddCodeFile(code);
var project = SurabWorkspace.CreateInMemoryProject(vfs);
var tree = project.GetSyntaxTree(codeFile);
var reasoner = project.GetReasoner(codeFile);

var diags = reasoner.EnsureTypeCheckAndGetAllDiags();
if (diags.Any(x => x.Level >= DiagLevel.Error))
{
	var errors = diags.Select(d => (d, v: d.Visualize(code))).ToList();
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
