using Surab.Analyzer;
using Surab.Compiler;

var src = """
    function print(v: Number): None {
      // Compiler intrinsic
    }

    function main(): None {
      let a: Number = 2;
      let b: Number = 3;
      let c: Number = a + b;
      print(c);
    }
    """;
var tree = Parser.ParseModule(src);

var model = SemanticModel.Create(tree);
var diags = model.GetAllDiagnostics();
if (diags.Any(x => x.Level >= DiagnosticLevel.Error))
{
    var errors = diags.Select(d => (d, v: d.Visualize(src))).ToList();
    return;
}

var bytecode = SurabCompiler.Compile(model);

// TODO: Tell vm to run the bytecode.

Console.WriteLine(bytecode);
