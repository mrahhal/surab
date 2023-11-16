using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Surab.Analysis;

public class SemanticModel
{
    private readonly Dictionary<SyntaxNode, Binder> _nodeToBinderMap = [];
    private readonly Dictionary<SyntaxNode, Symbol> _nodeToSymbolMap = [];

    private SemanticModel(SyntaxTree tree)
    {
        Tree = tree;
    }

    public SyntaxTree Tree { get; }

    // TODO: internal
    public static SemanticModel Create(SyntaxTree tree)
    {
        return Builder.Build(tree);
    }

    /// <summary>
    /// Tries getting a <see cref="Symbol"/> that is declared directly on the given node.
    /// </summary>
    public bool TryGetDeclaredSymbol(SyntaxNode node, [NotNullWhen(true)] out Symbol? symbol)
    {
        return _nodeToSymbolMap.TryGetValue(node, out symbol);
    }

    /// <summary>
    /// Gets a <see cref="Symbol"/> that is declared directly on the given node.
    /// Returns null if there's no declared symbol.
    /// </summary>
    public Symbol? GetDeclaredSymbol(SyntaxNode node)
    {
        TryGetDeclaredSymbol(node, out var symbol);
        return symbol;
    }

    /// <summary>
    /// Gets the first <see cref="Symbol"/> found when climbing up the tree starting and including the given node.
    /// </summary>
    public Symbol? GetEnclosingSymbol(SyntaxNode? node)
    {
        for (var current = node; current != null; current = current.Parent)
        {
            if (TryGetDeclaredSymbol(current, out var symbol))
            {
                return symbol;
            }
        }
        return null;
    }

    /// <summary>
    /// Tries getting a <see cref="Binder"/> that is defined directly on the given node.
    /// </summary>
    internal bool TryGetBinder(SyntaxNode node, [NotNullWhen(true)] out Binder? binder)
    {
        return _nodeToBinderMap.TryGetValue(node, out binder);
    }

    /// <summary>
    /// Gets the first <see cref="Binder"/> found when climbing up the tree starting and including the given node.
    /// </summary>
    internal Binder? GetEnclosingBinder(SyntaxNode? node)
    {
        for (var current = node; current != null; current = current.Parent)
        {
            if (TryGetBinder(current, out var binder))
            {
                return binder;
            }
        }
        return null;
    }

    /// <summary>
    /// Gets all diagnostics (syntax and semantic).
    /// </summary>
    public IEnumerable<Diagnostic> GetAllDiagnostics()
    {
        // Syntax
        var errored = Tree.Diagnostics.Diagnostics.Any(x => x.Level >= DiagnosticLevel.Error);
        if (errored)
        {
            // Short circuit
            return Tree.Diagnostics.Diagnostics;
        }

        // Semantic
        var semanticDiagnostics = SemanticAnalyzer.Analyze(this);
        return Tree.Diagnostics.Diagnostics.Concat(semanticDiagnostics.Diagnostics);
    }

    /// <summary>
    /// Looks up the <see cref="Symbol"/>, if any, that is referred to in the given node.
    /// </summary>
    public Symbol? LookupSymbol(SyntaxNode node)
    {
        var result = node switch
        {
            IdentifierExprNode n => LookupSymbol(n),
            TypeNode n => LookupSymbol(n.Identifier, n),
            ArgNode n => LookupSymbol(n.Expr),
            //CallExprNode n => LookupSymbol(n.Callee),

            _ => null,
        };

        return result ?? LookupType(node);
    }

    public Symbol? LookupSymbol(IdentifierExprNode node)
    {
        return LookupSymbol(node.Identifier, node);
    }

    private Symbol? LookupSymbol(IdentifierToken identifier, SyntaxNode node)
    {
        var binder = GetEnclosingBinder(node);
        return binder?.LookupIdentifier(identifier) ?? LookupBuiltIn(identifier);
    }

    internal static TypeSymbol? LookupBuiltIn(IdentifierToken identifier)
    {
        return identifier.Value switch
        {
            "None" => BuiltInTypeSystem.None,
            "Number" => BuiltInTypeSystem.Number,
            "Char" => BuiltInTypeSystem.Char,
            //"String" => BuiltInTypeSystem.String,
            "Bool" => BuiltInTypeSystem.Bool,

            _ => null,
        };
    }

    /// <summary>
    /// Looks up the <see cref="TypeSymbol"/>, if any, that represents the given node.
    /// </summary>
    public TypeSymbol? LookupType(SyntaxNode? node)
    {
        if (node == null) return null;

        return node switch
        {
            BareExprStmtNode n => LookupType(n),
            ExprNode n => LookupType(n),
            ArgNode n => LookupType(n.Expr),

            // For convenience.
            EqualsValueClauseNode n => LookupType(n.Expr),

            _ => null,
        };
    }

    private TypeSymbol? LookupType(BareExprStmtNode node)
    {
        return LookupType(node.Expr);
    }

    private TypeSymbol? LookupType(ExprNode node)
    {
        return node switch
        {
            BlockExprNode n => LookupType(n),
            AssignmentExprNode n => LookupType(n.Lhs),
            UnaryExprNode n => LookupType(n.Expr),
            BinaryExprNode n => LookupType(n),
            GroupingExprNode n => LookupType(n.Expr),
            CallExprNode n => LookupType(n),
            AccessExprNode n => LookupType(n),
            LiteralExprNode n => LookupType(n),
            IdentifierExprNode n => LookupType(n),
            ThisExprNode n => LookupType(n),

            _ => null,
        };
    }

    private TypeSymbol? LookupType(BlockExprNode node)
    {
        var lastStmt = node.Stmts.LastOrDefault();
        if (lastStmt == null)
        {
            return null;
        }

        return LookupType(lastStmt);
    }

    private TypeSymbol? LookupType(BinaryExprNode node)
    {
        var leftType = LookupType(node.Left);
        var rightType = LookupType(node.Right);

        if (leftType != null && rightType != null && leftType != rightType)
        {
            // TODO: ?
        }

        return leftType;
    }

    private TypeSymbol? LookupType(CallExprNode node)
    {
        var calleeSymbol = LookupSymbol(node.Callee);
        return calleeSymbol is FnSymbol fnSymbol ? fnSymbol.ReturnType : null;
    }

    private TypeSymbol? LookupType(AccessExprNode node)
    {
        var targetSymbol = LookupSymbol(node.Target);
        return targetSymbol switch
        {
            NamedTypeSymbol s => GetMemberTypeSymbol(s),

            _ => null,
        };

        TypeSymbol? GetMemberTypeSymbol(NamedTypeSymbol namedTypeSymbol)
        {
            // TODO: Make comparing identifiers better.
            var member = namedTypeSymbol.Members.FirstOrDefault(m => m.Name == node.Identifier.Value);
            if (member == null) return null;
            return member switch
            {
                // TODO: Make this Type optionally accessible from the Symbol type itself?
                ConstSymbol s => s.Type,
                FieldSymbol s => s.Type,

                _ => null,
            };
        }
    }

    private static TypeSymbol? LookupType(LiteralExprNode node)
    {
        return node.Literal.LiteralTokenKind switch
        {
            LiteralTokenKind.Number => BuiltInTypeSystem.Number,
            LiteralTokenKind.Char => BuiltInTypeSystem.Char,
            //LiteralTokenKind.String => BuiltInTypeSystem.String,
            LiteralTokenKind.Bool => BuiltInTypeSystem.Bool,

            _ => null,
        };
    }

    private TypeSymbol? LookupType(IdentifierExprNode node)
    {
        var binder = GetEnclosingBinder(node);
        var symbol = binder?.LookupIdentifier(node.Identifier);

        return symbol switch
        {
            TypeSymbol s => s,
            ConstSymbol s => s.Type,
            FieldSymbol s => s.Type,
            ParamSymbol s => s.Type,
            LocalSymbol s => s.Type,

            _ => null,
        };
    }

    private TypeSymbol? LookupType(ThisExprNode node)
    {
        var binder = GetEnclosingBinder(node);
        return binder?.LookupThis();
    }

    public TypeSymbol? InferTypeOfDeclaringNode(SyntaxNode node)
    {
        return node switch
        {
            LetStmtNode letStmt => LookupType(letStmt.Initializer),
            _ => null,
        };
    }

    private class Builder : Visitor
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

        public override void Visit(SyntaxNode node)
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

            Walk(node);

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
                ConstDeclNode n => new ConstSymbol(n),
                FnDeclNode n => new FnSymbol(n.Sig),
                MethodDeclNode n => new FnSymbol(n.Sig),
                FieldDeclNode n => new FieldSymbol(n),
                ClassDeclNode n => new NamedTypeSymbol(n),

                LetStmtNode n => new LocalSymbol(n),

                ParamNode n => new ParamSymbol(n),

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
                ModuleNode n => new ModuleBinder(n),
                ClassDeclNode n => new ClassDeclBinder(n, (NamedTypeSymbol)symbol!),
                FnDeclNode n => new FnBinder(n.Sig, (FnSymbol)symbol!),
                MethodDeclNode n => new FnBinder(n.Sig, (FnSymbol)symbol!),
                BlockExprNode n => new BlockExprBinder(n),

                _ => null,
            };
        }
    }
}
