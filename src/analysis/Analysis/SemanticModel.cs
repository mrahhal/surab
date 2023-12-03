using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;

namespace Surab.Analysis;

public class SemanticModel
{
    private readonly Dictionary<SyntaxNode, Scope> _nodeToScopeMap = [];
    private readonly Dictionary<SyntaxNode, Symbol> _nodeToSymbolMap = [];
    // TODO: Should be moved to some place above a semantic model so they're shared between different models?
    private readonly Dictionary<FnTypeKey, FnTypeSymbol> _unnamedFnTypeMap = [];

    private SemanticModel(SyntaxTree tree)
    {
        Tree = tree;
    }

    static SemanticModel()
    {
        // The first moment we touch SemanticModel in any way, we'll need to load the prelude.
        Prelude.Load();
    }

    /// <summary>
    /// Can be called from test environments to ensure prelude is loaded.
    /// </summary>
    internal static void EnsurePreludeLoaded()
    {
        // noop, just to ensure run the static ctor.
    }

    public SyntaxTree Tree { get; }

    // TODO: internal?
    public static SemanticModel Create(SyntaxTree tree, bool isPrelude = false)
    {
        return Builder.Build(tree, isPrelude);
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
    /// Tries getting a <see cref="Scope"/> that is defined directly on the given node.
    /// </summary>
    internal bool TryGetScope(SyntaxNode node, [NotNullWhen(true)] out Scope? scope)
    {
        return _nodeToScopeMap.TryGetValue(node, out scope);
    }

    /// <summary>
    /// Gets the first <see cref="Scope"/> found when climbing up the tree starting and including the given node.
    /// </summary>
    internal Scope? GetEnclosingScope(SyntaxNode? node)
    {
        for (var current = node; current != null; current = current.Parent)
        {
            if (TryGetScope(current, out var scope))
            {
                return scope;
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
        return [.. Tree.Diagnostics.Diagnostics, .. semanticDiagnostics.Diagnostics];
    }

    /// <summary>
    /// Looks up the <see cref="Symbol"/>, if any, that is referred to in the given node.
    /// </summary>
    public Symbol? LookupSymbol(SyntaxNode node)
    {
        var result = node switch
        {
            IdentifierExprNode n => LookupSymbol(n),
            NamedTypeNode n => LookupSymbol(n.Identifier, n),
            FnTypeNode n => LookupSymbol(n),
            ArgNode n => LookupSymbol(n.Expr),
            //CallExprNode n => LookupSymbol(n.Callee),
            AccessExprNode n => LookupSymbol(n),
            StructCreationExprNode n => LookupSymbol(n.Identifier, n),

            _ => null,
        };

        return result ?? LookupType(node);
    }

    public Symbol? LookupSymbol(IdentifierExprNode node)
    {
        var scope = ResolveScopeForIdentifier(node);
        return LookupSymbol(node.Identifier, scope);
    }

    private Symbol? LookupSymbol(IdentifierToken identifier, SyntaxNode node)
    {
        var scope = GetEnclosingScope(node);
        return LookupSymbol(identifier, scope);
    }

    private Symbol? LookupSymbol(IdentifierToken identifier, Scope? scope)
    {
        return scope?.LookupIdentifier(identifier);
    }

    internal FnTypeSymbol? LookupSymbol(FnTypeNode node)
    {
        return GetUnnamedFnTypeSymbol(node);
    }

    private Symbol? LookupSymbol(AccessExprNode node)
    {
        // Lookup the field/etc symbol in the access expr.

        var targetSymbol = LookupSymbol(node.Target); // TODO: Remove this?
        Debug.Assert(targetSymbol != null);

        // This might need an update if we have access exprs for other than types later.
        var targetType = LookupType(node.Target);
        Debug.Assert(targetType != null);

        return targetType.Scope.LookupIdentifierHere(node.Identifier);
    }

    /// <summary>
    /// Looks up an exported <see cref="Symbol"/> matching the given name.
    /// </summary>
    public Symbol? LookupExportedSymbolByName(string name)
    {
        // Should be the module's scope.
        var scope = GetEnclosingScope(Tree.Root);
        return scope?.LookupExportedIdentifier(name);
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
            UnaryExprNode n => LookupType(n),
            BinaryExprNode n => LookupType(n),
            GroupingExprNode n => LookupType(n.Expr),
            CallExprNode n => LookupType(n),
            AccessExprNode n => LookupType(n),
            LiteralExprNode n => LookupType(n),
            IdentifierExprNode n => LookupType(n),
            ThisExprNode n => LookupType(n),
            StructCreationExprNode n => LookupType(n),

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

    private TypeSymbol? LookupType(UnaryExprNode node)
    {
        return LookupType(node.Expr);
    }

    private TypeSymbol? LookupType(BinaryExprNode node)
    {
        return LookupType(node.Left);
    }

    private TypeSymbol? LookupType(CallExprNode node)
    {
        var fnType = LookupType(node.Callee) as FnTypeSymbol;
        return fnType?.ReturnType;
    }

    private TypeSymbol? LookupType(AccessExprNode node)
    {
        var targetSymbol = LookupSymbol(node.Target);
        return targetSymbol switch
        {
            TypeSymbol s => GetMemberTypeSymbol(s), // Static access
            ISymbolHasType s => GetMemberTypeSymbol(s.Type), // Instance access
            _ => null,
        };

        TypeSymbol? GetMemberTypeSymbol(TypeSymbol typeSymbol)
        {
            var member = typeSymbol.Scope.LookupIdentifierHere(node.Identifier);
            return member switch
            {
                ISymbolHasType s => s.Type,
                _ => null,
            };
        }
    }

    private static TypeSymbol? LookupType(LiteralExprNode node)
    {
        return node.Literal.LiteralTokenKind switch
        {
            LiteralTokenKind.NumericInteger => LookupNumericInteger(),
            LiteralTokenKind.NumericFloat => LookupNumericFloat(),
            LiteralTokenKind.Char => BuiltinTypeSystem.@char,
            //LiteralTokenKind.String => BuiltInTypeSystem.String,
            LiteralTokenKind.Bool => BuiltinTypeSystem.@bool,

            _ => null,
        };

        TypeSymbol LookupNumericInteger()
        {
            var explicitType = BuiltinTypeSystem.GetNumericTypeFromString(node.Literal.NumericType);
            if (explicitType != null)
                return explicitType;

            // TODO: How to account for integers that might end up being signed?
            Debug.Assert(node.Literal.TryGetNumericIntegerValue(out var value));
            if (value <= int.MaxValue)
                return BuiltinTypeSystem.i32;
            else
                return BuiltinTypeSystem.i64;
        }

        TypeSymbol LookupNumericFloat()
        {
            var explicitType = BuiltinTypeSystem.GetNumericTypeFromString(node.Literal.NumericType);
            if (explicitType != null)
                return explicitType;

            // Floats are inferred as f64 by default.
            return BuiltinTypeSystem.f64;
        }
    }

    private TypeSymbol? LookupType(IdentifierExprNode node)
    {
        var scope = ResolveScopeForIdentifier(node);
        return LookupType(node.Identifier, scope);
    }

    private TypeSymbol? LookupType(IdentifierToken identifier, SyntaxNode node)
    {
        var scope = GetEnclosingScope(node);
        return LookupType(identifier, scope);
    }

    private TypeSymbol? LookupType(IdentifierToken identifier, Scope? scope)
    {
        var symbol = scope?.LookupIdentifier(identifier);

        return symbol switch
        {
            TypeSymbol s => s,
            ISymbolHasType s => s.Type,
            _ => null,
        };
    }

    private TypeSymbol? LookupType(ThisExprNode node)
    {
        var scope = GetEnclosingScope(node);
        return scope?.LookupThis();
    }

    private TypeSymbol? LookupType(StructCreationExprNode node)
    {
        return LookupType(node.Identifier, node);
    }

    /// <summary>
    /// Resolves the correct <see cref="Scope"/> to resolve identifier symbol/type.
    /// Resolves to either the containing scope, a type, etc, depending on what the identifier really means (e.g. local identifier vs identifier inside MapInitializer).
    /// </summary>
    private Scope? ResolveScopeForIdentifier(IdentifierExprNode identifierExpr)
    {
        if (identifierExpr.Parent is AssignmentExprNode assignmentExpr &&
            assignmentExpr.Parent is MapInitializerNode mapInitializer &&
            mapInitializer.Parent is StructCreationExprNode structCreationExpr)
        {
            // Creating a struct: `S1 { a = 42 }`
            // Identifier `a` should return the S1 struct's scope.
            var structType = LookupType(structCreationExpr);
            return structType?.Scope;
        }
        return GetEnclosingScope(identifierExpr);
    }

    public TypeSymbol? InferTypeOfDeclaringNode(SyntaxNode node)
    {
        return node switch
        {
            VarStmtNode letStmt => LookupType(letStmt.Initializer),
            FieldDeclNode fieldDecl => LookupType(fieldDecl.Initializer),
            _ => null,
        };
    }

    // Supported:
    // - FnDeclNode: From `fn foo(v: i32) i32 { ... }`
    // - MethodDeclNode: ^ but method
    // - TypeNode: e.g. From the type in `var f: fn (i32) i32;`
    // - AliasNode: `alias Delegate fn (i32) i32;`
    public FnTypeSymbol GetFnType(SyntaxNode node)
    {
        return node switch
        {
            FnDeclNode fnDecl => GetUnnamedFnTypeSymbol(fnDecl.Sig),
            MethodDeclNode methodDecl => GetUnnamedFnTypeSymbol(methodDecl.Sig),
            FnTypeNode fnType => GetUnnamedFnTypeSymbol(fnType),

            // Create an fn type symbol that is for this named alias type.
            // TODO: From alias. Will use _nodeToSymbolMap, symbol should already be created from builder.
            //case AliasDeclNode aliasDecl:

            _ => throw new UnreachableException(),
        };
    }

    private FnTypeSymbol GetUnnamedFnTypeSymbol(FnSigNode sig)
    {
        var key = CreateFnTypeKey(sig);
        return GetOrCreateUnnamedFnType(key);
    }

    private FnTypeSymbol GetUnnamedFnTypeSymbol(FnTypeNode fnType)
    {
        var key = CreateFnTypeKey(fnType);
        return GetOrCreateUnnamedFnType(key);
    }

    private FnTypeSymbol CreateUnnamedFnTypeSymbol(FnTypeKey key)
    {
        return new FnTypeSymbol(null, key);
    }

    private FnTypeSymbol GetOrCreateUnnamedFnType(FnTypeKey key)
    {
        if (_unnamedFnTypeMap.TryGetValue(key, out var symbol))
        {
            return symbol;
        }

        symbol = CreateUnnamedFnTypeSymbol(key);
        return _unnamedFnTypeMap[key] = symbol;
    }

    private FnTypeKey CreateFnTypeKey(FnSigNode sig)
    {
        // TODO: Return something else in case of error diags? (p.Type below could be null)
        var isExtern = sig.Attrs.HasAttr(KnownAttrs.Extern);
        return new FnTypeKey(
            isExtern,
            sig.ReturnType == null ? BuiltinTypeSystem.None : (TypeSymbol)LookupSymbol(sig.ReturnType)!,
            sig.Params.Params.Select(p => (TypeSymbol)LookupSymbol(p.Type!.Type)!));
    }

    private FnTypeKey CreateFnTypeKey(FnTypeNode fnType)
    {
        var isExtern = fnType.Attrs.HasAttr(KnownAttrs.Extern);
        return new FnTypeKey(
            isExtern,
            fnType.ReturnType == null ? BuiltinTypeSystem.None : (TypeSymbol)LookupSymbol(fnType.ReturnType)!,
            fnType.ParamTypes.ParamTypes.Select(p => (TypeSymbol)LookupSymbol(p.Type)!));
    }

    private class Builder(SyntaxTree tree, bool isPrelude) : Visitor
    {
        private class TempTypeContext
        {
            public int FieldCounter = 0;
        }

        private readonly SemanticModel _model = new(tree);
        private readonly Dictionary<SyntaxNode, Symbol> _nodeToSymbolMap = [];
        private Scope? _parentScope;
        private Symbol? _parentSymbol;
        private TempTypeContext? _typeContext;

        public static SemanticModel Build(SyntaxTree tree, bool isPrelude)
        {
            var builder = new Builder(tree, isPrelude);
            builder.Build();
            return builder._model;
        }

        private void Build()
        {
            if (isPrelude)
            {
                // For the prelude, we'll especially go over the top level decls first to fill in BuiltinTypeSystem first thing before doing anything else.
                // Lots of other processes here depends on BuiltinTypeSystem being initialized.

                Debug.Assert(tree.Root is ModuleNode);
                var moduleNode = (ModuleNode)tree.Root;
                foreach (var decl in moduleNode.Decls)
                {
                    var symbol = GetOrCreateSymbolOnNode(decl);
                    Debug.Assert(symbol != null);
                    BuiltinTypeSystem.TryRegisterBuiltinSymbol(symbol);
                }
            }

            Visit(tree.Root);
        }

        public override void Visit(SyntaxNode node)
        {
            var symbol = CreateSymbol(node);
            var scope = CreateScope(node, symbol);
            var typeContext = CreateTypeContext(node);

            if (symbol != null)
            {
                var contextScopeForSymbol = scope ?? _parentScope;
                Debug.Assert(contextScopeForSymbol != null, "Unexpected state when there's a symbol but no context scope for it.");
                symbol.SetContextScope(contextScopeForSymbol);
            }

            var prevParentSymbol = _parentSymbol;
            if (symbol != null)
            {
                _parentSymbol = symbol;
            }
            var prevParentScope = _parentScope;
            if (scope != null)
            {
                _parentScope = scope;
            }
            var prevTypeContext = _typeContext;
            if (typeContext != null)
            {
                _typeContext = typeContext;
            }

            Walk(node);

            _parentSymbol = prevParentSymbol;
            _parentScope = prevParentScope;
            _typeContext = prevTypeContext;
        }

        private Symbol? CreateSymbol(SyntaxNode node)
        {
            var symbol = GetOrCreateSymbolOnNode(node);
            if (symbol != null)
            {
                symbol.SetParent(_parentSymbol);
                _model._nodeToSymbolMap[node] = symbol;
            }
            return symbol;
        }

        private Symbol? GetOrCreateSymbolOnNode(SyntaxNode node)
        {
            if (_nodeToSymbolMap.TryGetValue(node, out var symbol))
                return symbol;

            symbol = node switch
            {
                ConstDeclNode n => new ConstSymbol(n),
                FnDeclNode n => new FnSymbol(n.Sig),
                MethodDeclNode n => new FnSymbol(n.Sig),
                FieldDeclNode n => new FieldSymbolImpl(n, _typeContext == null ? -1 : _typeContext.FieldCounter++),
                StructDeclNode n => new StructTypeSymbol(n),
                ClassDeclNode n => new ClassTypeSymbol(n),

                VarStmtNode n => new LocalSymbol(n),

                ParamNode n => new ParamSymbol(n),

                _ => null,
            };

            if (symbol != null)
            {
                _nodeToSymbolMap[node] = symbol;
            }

            return symbol;
        }

        private Scope? CreateScope(SyntaxNode node, Symbol? symbol)
        {
            var scope = CreateScopeOnNode(node, symbol);
            if (scope != null)
            {
                scope.SemanticModel = _model;
                scope.SetParent(_parentScope);
                _model._nodeToScopeMap[node] = scope;
            }
            return scope;
        }

        private Scope? CreateScopeOnNode(SyntaxNode node, Symbol? symbol)
        {
            return node switch
            {
                // Temporarily give non prelude modules the prelude model to lookup exported symbols.
                // Later, this should be given as part of the imported modules.
                ModuleNode n => new ModuleScope(n, isPrelude ? null : Prelude.GetSemanticModel()),
                StructDeclNode n => new StructDeclScope(n, (StructTypeSymbol)symbol!),
                ClassDeclNode n => new ClassDeclScope(n, (NamedTypeSymbol)symbol!),
                FnDeclNode n => new FnScope(n.Sig, (FnSymbol)symbol!),
                MethodDeclNode n => new FnScope(n.Sig, (FnSymbol)symbol!),
                BlockExprNode n => new BlockExprScope(n),

                _ => null,
            };
        }

        private static TempTypeContext? CreateTypeContext(SyntaxNode node)
        {
            return node switch
            {
                StructDeclNode or ClassDeclNode => new TempTypeContext(),

                _ => null,
            };
        }
    }
}
