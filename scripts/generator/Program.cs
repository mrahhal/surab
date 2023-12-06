using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using MR.AttributeDI.ServiceCollection;

var srcFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "../../src");

var services = new ServiceCollection();
services.ConfigureFromAttributes(Assembly.GetExecutingAssembly());
var provider = services.BuildServiceProvider();

await Generate();

async Task Generate()
{
    await GenerateAst();
    await GenerateAstVisitors();
}

async Task GenerateAst()
{
    var generatedFilePath = Path.Combine(srcFolderPath, "analysis/Parsing/Ast.g.cs");
    var generator = provider.GetRequiredService<AstGenerator>();
    await generator.GenerateToAsync(generatedFilePath);
}

async Task GenerateAstVisitors()
{
    var generatedFilePath = Path.Combine(srcFolderPath, "analysis/Parsing/SyntaxTreeVisitor.g.cs");
    var generator = provider.GetRequiredService<AstVisitorsGenerator>();
    await generator.GenerateToAsync(generatedFilePath);
}
