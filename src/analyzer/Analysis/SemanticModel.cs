using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Surab.Analyzer;

public class SemanticModel
{
    private readonly Dictionary<SyntaxNode, Binder> _nodeToBinderMap = new();
    private readonly Dictionary<SyntaxNode, Symbol> _nodeToSymbolMap = new();

    public SemanticModel(SyntaxTree tree)
    {
        Tree = tree;
    }

    public SyntaxTree Tree { get; }

    internal static SemanticModel Create(SyntaxTree tree)
    {
        return Builder.Build(tree);
    }

    public bool TryGetSymbol(SyntaxNode node, [MaybeNullWhen(false)] out Symbol symbol)
    {
        return _nodeToSymbolMap.TryGetValue(node, out symbol);
    }

    internal bool TryGetBinder(SyntaxNode node, [MaybeNullWhen(false)] out Binder binder)
    {
        return _nodeToBinderMap.TryGetValue(node, out binder);
    }

    private class Builder : IVisitor
    {
        private readonly SemanticModel _model;
        private Binder? _parentBinder;
        private Symbol? _parentSymbol;

        public Builder(SyntaxTree tree)
        {
            _model = new(tree);
        }

        public static SemanticModel Build(SyntaxTree tree)
        {
            var builder = new Builder(tree);
            builder.Visit(tree.Root);
            return builder._model;
        }

        public void Visit(SyntaxNode node)
        {
            var symbol = CreateSymbol(node);
            var binder = CreateBinder(node, symbol);

            if (symbol != null)
            {
                var contextBinderForSymbol = binder ?? _parentBinder;
                Debug.Assert(contextBinderForSymbol != null, "Unexpected state when there's a symbol but no context binder for it.");
                symbol.SetContextBinder(contextBinderForSymbol);
            }

            var prevParentSymbol = _parentSymbol;
            if (symbol != null)
            {
                _parentSymbol = symbol;
            }
            var prevParentBinder = _parentBinder;
            if (binder != null)
            {
                _parentBinder = binder;
            }

            ((IVisitor)this).Walk(node);

            if (prevParentSymbol != null)
            {
                _parentSymbol = prevParentSymbol;
            }
            if (binder != null)
            {
                _parentBinder = prevParentBinder;
            }
        }

        private Symbol? CreateSymbol(SyntaxNode node)
        {
            var symbol = CreateSymbolOnNode(node);
            if (symbol != null)
            {
                symbol.SetParent(_parentSymbol);
                _model._nodeToSymbolMap[node] = symbol;
            }
            return symbol;
        }

        private static Symbol? CreateSymbolOnNode(SyntaxNode node)
        {
            return node switch
            {
                ConstDeclarationNode constDeclaration => new ConstSymbol(constDeclaration),
                FunctionDeclarationNode functionDeclaration => new FunctionSymbol(functionDeclaration.Signature),
                MethodDeclarationNode methodDeclaration => new FunctionSymbol(methodDeclaration.Signature),
                FieldDeclarationNode fieldDeclaration => new FieldSymbol(fieldDeclaration),
                ClassDeclarationNode classDeclaration => new NamedTypeSymbol(classDeclaration),

                LetStatementNode letStatement => new LocalSymbol(letStatement),

                _ => null,
            };
        }

        private Binder? CreateBinder(SyntaxNode node, Symbol? symbol)
        {
            var binder = CreateBinderOnNode(node, symbol);
            if (binder != null)
            {
                binder.SemanticModel = _model;
                binder.SetParent(_parentBinder);
                _model._nodeToBinderMap[node] = binder;
            }
            return binder;
        }

        private static Binder? CreateBinderOnNode(SyntaxNode node, Symbol? symbol)
        {
            return node switch
            {
                ModuleNode module => new ModuleBinder(module),
                ClassDeclarationNode classDeclaration => new ClassDeclarationBinder(classDeclaration, (NamedTypeSymbol)symbol!),
                FunctionSignatureNode functionSignature => new FunctionSignatureBinder(functionSignature, (FunctionSymbol)symbol!),
                BlockExpressionNode block => new BlockExpressionBinder(block),

                _ => null,
            };
        }
    }
}
