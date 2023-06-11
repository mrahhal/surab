using Surab.Analyzer;
using Surab.Compiler;

var tree = Parser.ParseModule("""
    function main(): Number {
      let a: Number = 2;
      let b: Number = 3;
      let c: Number = a + b;
      return c;
      //print(c);
    }
    """);
var diags1 = tree.Diagnostics;
var model = SemanticModel.Create(tree);
var diags2 = model.GetAllDiagnostics();
var bytecode = SurabCompiler.Compile(model);

Console.WriteLine(bytecode);
