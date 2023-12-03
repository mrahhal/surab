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
        print_i32(number);
    }

    //fn foo2(number: i32) {
    //    print_i32(number + 3);
    //}

    fn main() {
        var f: fn (i32) = foo1;
        //var number = 40;
        f(40);
        //f = foo2;
        //f(number);
        foo1(3222);
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
