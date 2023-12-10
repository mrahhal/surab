using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Server;
using Surab.Analysis;

namespace Surab.LSPServer;

public class SurabSolver(LanguageServerHost host)
{
    private readonly LanguageServerHost _host = host;

    public LanguageServerHost Host => _host;

    public LanguageServer Server => Host.Server;

    public SurabProject Project => Host.SurabProject;

    public SourceFile? GetSourceFile(TextDocumentIdentifier identifier) => GetSourceFile(identifier.Uri);

    public SourceFile? GetSourceFile(DocumentUri uri)
    {
        var filePath = uri.GetFileSystemPath();
        return Host.SurabProject.SourceFiles.FirstOrDefault(x => x.Path == filePath);
    }

    public SyntaxNode? GetDeepestNodeAt(SourceFile sourceFile, LinePosition linePosition)
    {
        var tree = Project.GetSyntaxTree(sourceFile);
        return GetDeepestNodeAtRecursive(tree.Root, linePosition);
    }

    private SyntaxNode? GetDeepestNodeAtRecursive(SyntaxNode node, LinePosition linePosition)
    {
        foreach (var childNode in node.ChildNodes)
        {
            if (childNode.Span.Line.Contains(linePosition))
            {
                return GetDeepestNodeAtRecursive(childNode, linePosition);
            }
        }
        return node;
    }
}
