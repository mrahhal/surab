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
    fn foo1(number: i32) {
        print_i32(number + 2);
    }

    fn foo2(number: i32) {
        print_i32(number + 3);
    }

    fn main() {
        var number = 40;
        var f: fn (i32) = foo1;
        f(number);
        f = foo2;
        f(number);
    }
    """;
var tree = Parser.ParseModule(src);

var model = SemanticModel.Create(tree);
var diags = model.GetAllDiagnostics();
if (diags.Any(x => x.Level >= DiagnosticLevel.Error))
{
    var errors = diags.Select(d => (d, v: d.Visualize(src))).ToList();
    ConsoleHelper.WriteError("ERROR COMPILING\n");
    Console.WriteLine(string.Join('\n', errors.Select(x => x.v)));
    return;
}

var exe = Compiler.Compile(model, "");
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
