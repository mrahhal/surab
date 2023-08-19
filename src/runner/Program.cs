using System.Diagnostics;
using Surab.Analysis;
using Surab.Compilation;

var src = """
    fn print(v: Number) {
        // Compiler intrinsic
    }

    fn noop() { }

    fn foo(x: Number, y: Number): Number {
        let e = x + y;
        return e + 1;
        //return x + y;
    }

    fn main() {
        let a = 2;
        noop();
        let b: Number = 4;
        let c = foo(a, b);
        c = 999.2;
        print(c);
        print(41 + 1);
    }
    """;
var tree = Parser.ParseModule(src);

var model = SemanticModel.Create(tree);
var diags = model.GetAllDiagnostics();
if (diags.Any(x => x.Level >= DiagnosticLevel.Error))
{
    var errors = diags.Select(d => (d, v: d.Visualize(src))).ToList();
    Console.WriteLine("[RED] ERROR COMPILING\n");
    Console.WriteLine(string.Join('\n', errors.Select(x => x.v)));
    return;
}

var exe = Compiler.Compile(model, "");

var p = new Process
{
    StartInfo =
    {
        FileName = exe,
        Arguments = "",
    }
};

p.Start();
p.WaitForExit();
