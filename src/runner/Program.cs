using System.Diagnostics;
using Surab;
using Surab.Analysis;
using Surab.Compilation;

var src = """
    struct S1 {
        a: Number;
        b: Bool;
    }

    struct S2 {
        a: Number;
    }

    extern fn print_from_rust(s2: S2);

    fn print(v: Number) {
        // Compiler intrinsic
    }

    fn printb(b: Bool) {
        // Compiler intrinsic
    }

    fn noop() { }

    fn f(x: Number, y: Number): Number {
        var e = x + y;
        return e + 1;
        //return x + y;
    }

    fn fs(s1: S1, s2: S2) {
        print(s1.a);
        print(s2.a);
    }

    fn main() {
        var a = 2;
        noop();
        var b: Number = 4;
        var c = f(a, b);
        c = 999.2;
        print(c);
        print(41 + 1);

        var s1 = S1 {
            a = 82,
            b = true,
        };
        print(s1.a);
        s1.a = 83;
        print(s1.a);
        printb(s1.b);

        var s2 = S2 { a = 42 };

        fs(s1, s2);

        print_from_rust(s2);
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
