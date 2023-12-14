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
    alias Delegate1 as @extern fn (S2);

    struct S1 {
        a: f64;
        b: bool;
    }

    @extern
    struct S2 {
        a: f64;
    }

    @extern
    fn print_from_rust(s2: S2);

    fn noop() { }

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
        noop();
        var b: f64 = 4.0;
        var c = f(a, b);
        c = {
            var c2 = 999.0;
            c2 + 0.2
        };
        print_f64(c);
        print_i32(41 + 1);

        var s1 = S1 {
            a = 82.0,
            b = true,
        };
        print_f64(s1.a);
        s1.a = 83.0;
        print_f64(s1.a);
        print_bool(s1.b);

        var s2 = S2 { a = 42.3 };

        fs(s1, s2);

        print_from_rust(s2);

        // Fn pointers
        var fn_ptr /* : @extern fn (f64, f64) f64 */ = f;
        print_f64(fn_ptr(2.0, 3.0));

        var fn_ptr: @extern fn (S2) = print_from_rust;
        fn_ptr(s2);

        var fn_ptr: Delegate1 = print_from_rust;
        fn_ptr(s2);
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
