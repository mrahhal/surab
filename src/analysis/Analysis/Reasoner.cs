using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Surab.Analysis;

/// <summary>
/// A <see cref="Reasoner"/> is responsible for answering semantic questions about the <see cref="SyntaxTree"/> it's tied to.
/// It is the core of semantic analysis.
/// </summary>
public class Reasoner
{
	private readonly Dictionary<SyntaxNode, Scope> _nodeToScopeMap = [];
	private readonly Dictionary<SyntaxNode, Symbol> _nodeToSymbolMap = [];

	// Only set if this is the prelude reasoner. Needs some good refactoring.
	private Dictionary<KnownTypeTag, TypeSymbol>? _knownTypeTagToTypeSymbolMap;

	private Reasoner(SurabProject project, SyntaxTree tree)
	{
		Tree = tree;
		Project = project;
	}

	public SurabProject Project { get; }

	public SyntaxTree Tree { get; }

	public SourceFile SourceFile => Tree.SourceFile;

	private ReasoningContext ReasoningContext => Project.ReasoningContext;

	internal static Reasoner Create(SurabProject project, SyntaxTree tree)
	{
		return Builder.Build(project, tree);
	}

	internal static Reasoner CreatePrelude(SurabProject project, SyntaxTree tree)
	{
		return Builder.Build(project, tree, isPrelude: true);
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

	public Symbol? GetDeclaredSymbol(IdentifierToken identifierToken)
	{
		var node = identifierToken.GetParentNodeThatDeclaresSymbol();
		return node == null ? null : GetDeclaredSymbol(node);
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
		var parserDiagnostics = Tree.Diagnostics.Diagnostics;

		// Semantic
		var semanticDiagnostics = CoreAnalyzer.Analyze(this);

		return [.. parserDiagnostics, .. semanticDiagnostics.Diagnostics];
	}

	/// <summary>
	/// Looks up the <see cref="Symbol"/> that is referred to in the given node.
	/// If a symbol can't be found, `null` is returned.
	/// </summary>
	public Symbol? LookupSymbol(SyntaxNode? node)
	{
		return node switch
		{
			IdentifierExprNode n => LookupSymbol(n),
			ThisExprNode n => LookupSymbol(n),
			PtrTypeNode n => LookupSymbol(n),
			NamedTypeNode n => LookupSymbol(n.Identifier, n),
			FnTypeNode n => LookupSymbol(n),
			ArgNode n => LookupSymbol(n.Expr),
			//CallExprNode n => LookupSymbol(n.Callee),
			AccessExprNode n => LookupSymbol(n),
			StructCreationExprNode n => LookupSymbol(n.Identifier, n),

			_ => null,
		};
	}

	public Symbol? LookupSymbol(TypeNode? typeNode)
	{
		return typeNode switch
		{
			NamedTypeNode n => LookupSymbol(n.Identifier, n),
			FnTypeNode n => LookupSymbol(n),
			_ => null,
		};
	}

	public Symbol? LookupSymbol(IdentifierExprNode node)
	{
		if (TryResolveIdentifierExprSymbolInCreationExpr(node, out var symbol))
		{
			return symbol;
		}
		else
		{
			return LookupSymbol(node.Identifier, node);
		}
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

	private Symbol? LookupSymbol(ThisExprNode node)
	{
		return GetThisSymbol(node);
	}

	internal PtrTypeSymbol LookupSymbol(PtrTypeNode node)
	{
		return GetPtrType(node);
	}

	internal FnTypeSymbol LookupSymbol(FnTypeNode node)
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

		return targetType.GetMember(node.Identifier);
	}

	// TODO: What does "exported" mean in this context? (right now it means a public symbol in a module)
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
	/// Looks up the <see cref="TypeSymbol"/> that represents the given node.
	/// The result is never null. If type resolving fails then an <see cref="ErrorTypeSymbol"/> is returned.
	/// </summary>
	public TypeSymbol LookupType(SyntaxNode? node)
	{
		return node switch
		{
			BareExprStmtNode n => LookupType(n),
			ExprNode n => LookupType(n),
			ArgNode n => LookupType(n.Expr),
			EqualsExprClauseNode n => LookupType(n.Expr),

			// Should we support non expr nodes like these in LookupType?
			_ when node?.DeclaresSymbol() ?? false => LookupTypeFromDeclaredSymbol(node),

			_ => ErrorTypeSymbol.Create(),
		};
	}

	private TypeSymbol LookupTypeFromDeclaredSymbol(SyntaxNode node)
	{
		var symbol = GetDeclaredSymbol(node) as ISymbolHasType;
		return symbol?.Type ?? ErrorTypeSymbol.Create();
	}

	private TypeSymbol LookupType(BareExprStmtNode node)
	{
		return LookupType(node.Expr);
	}

	private TypeSymbol LookupType(ExprNode node)
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

			_ => ErrorTypeSymbol.Create(),
		};
	}

	private TypeSymbol LookupType(BlockExprNode node)
	{
		var lastStmt = node.Stmts.LastOrDefault();
		if (lastStmt == null)
		{
			return ErrorTypeSymbol.Create();
		}

		return LookupType(lastStmt);
	}

	private TypeSymbol LookupType(UnaryExprNode node)
	{
		var exprType = LookupType(node.Expr);
		return node.Op.UnaryOperatorTokenKind switch
		{
			UnaryOperatorTokenKind.Deref => exprType is PtrTypeSymbol ptrType ? ptrType.InnerType.AsTypeSymbolOrError() : exprType,
			UnaryOperatorTokenKind.Addr => GetPtrType(exprType),
			_ => exprType,
		};
	}

	private TypeSymbol LookupType(BinaryExprNode node)
	{
		return LookupType(node.Left);
	}

	private TypeSymbol LookupType(CallExprNode node)
	{
		var fnType = LookupType(node.Callee) as FnTypeSymbol;
		return fnType?.ReturnType ?? ErrorTypeSymbol.Create();
	}

	private TypeSymbol LookupType(AccessExprNode node)
	{
		var targetSymbol = LookupSymbol(node.Target);
		var result = targetSymbol switch
		{
			TypeSymbol s => GetMemberTypeSymbol(s), // Static access
			ISymbolHasType s => GetMemberTypeSymbol(s.Type), // Instance access
			_ => null,
		};
		return result ?? ErrorTypeSymbol.Create();

		TypeSymbol? GetMemberTypeSymbol(TypeSymbol typeSymbol)
		{
			if (typeSymbol.IsError) return ErrorTypeSymbol.Create();

			var member = typeSymbol.GetMember(node.Identifier);
			return member switch
			{
				ISymbolHasType s => s.Type,
				_ => null,
			};
		}
	}

	private TypeSymbol LookupType(LiteralExprNode node)
	{
		return node.Literal.LiteralTokenKind switch
		{
			LiteralTokenKind.NumericInteger => LookupNumericInteger(),
			LiteralTokenKind.NumericFloat => LookupNumericFloat(),
			LiteralTokenKind.Bool => Project.ResolveKnownTypeSymbol(KnownTypeTag.@bool),
			LiteralTokenKind.Char => Project.ResolveKnownTypeSymbol(KnownTypeTag.@char),
			LiteralTokenKind.String => Project.ResolveKnownTypeSymbol(KnownTypeTag.@string),

			_ => throw new UnreachableException(),
		};

		TypeSymbol LookupNumericInteger()
		{
			var explicitType = Project.ResolveKnownTypeSymbolFromName(node.Literal.NumericType);
			if (explicitType != null)
				return explicitType;

			// TODO: How to account for integers that might end up being signed?
			Debug.Assert(node.Literal.TryGetNumericIntegerValue(out var value));
			if (value <= int.MaxValue)
				return Project.ResolveKnownTypeSymbol(KnownTypeTag.i32);
			else
				return Project.ResolveKnownTypeSymbol(KnownTypeTag.i64);
		}

		TypeSymbol LookupNumericFloat()
		{
			var explicitType = Project.ResolveKnownTypeSymbolFromName(node.Literal.NumericType);
			if (explicitType != null)
				return explicitType;

			// Floats are inferred as f64 by default.
			return Project.ResolveKnownTypeSymbol(KnownTypeTag.f64);
		}
	}

	private TypeSymbol LookupType(IdentifierExprNode node)
	{
		if (TryResolveIdentifierExprSymbolInCreationExpr(node, out var symbol))
		{
			return (symbol as ISymbolHasType)?.Type ?? ErrorTypeSymbol.Create();
		}
		else
		{
			return LookupType(node.Identifier, node);
		}
	}

	private TypeSymbol LookupType(IdentifierToken identifier, SyntaxNode node)
	{
		var scope = GetEnclosingScope(node);
		return LookupType(identifier, scope);
	}

	private TypeSymbol LookupType(IdentifierToken identifier, Scope? scope)
	{
		var symbol = scope?.LookupIdentifier(identifier);

		return symbol switch
		{
			TypeSymbol s => s,
			ISymbolHasType s => s.Type,
			_ => ErrorTypeSymbol.Create(),
		};
	}

	private TypeSymbol LookupType(ThisExprNode node)
	{
		var thisSymbol = LookupSymbol(node) as ThisSymbol;
		return thisSymbol?.Type ?? ErrorTypeSymbol.Create();
	}

	private TypeSymbol LookupType(StructCreationExprNode node)
	{
		return LookupType(node.Identifier, node);
	}

	/// <summary>
	/// If the identifier expr is a part of setting a symbol in a creation expr, this method returns true and tries to resolve that symbol.
	/// Otherwise false is returned.
	/// </summary>
	private bool TryResolveIdentifierExprSymbolInCreationExpr(IdentifierExprNode identifierExpr, out Symbol? symbol)
	{
		symbol = null;
		// The identifier inside a creation expr: `S1 { a = 42 }`
		if (identifierExpr.Parent is AssignmentExprNode assignmentExpr &&
			assignmentExpr.Lhs == identifierExpr &&
			assignmentExpr.Parent is MapInitializerItemNode mapInitializerItem &&
			mapInitializerItem.Parent is MapInitializerNode mapInitializer &&
			mapInitializer.Parent is StructCreationExprNode structCreationExpr)
		{
			var structType = LookupType(structCreationExpr);
			symbol = structType?.GetCreationSymbol(identifierExpr.Identifier)?.Symbol;
			return true;
		}
		return false;
	}

	public TypeSymbol? InferTypeOfDeclaringNode(SyntaxNode node)
	{
		return node switch
		{
			VarStmtNode varStmt => LookupType(varStmt.Initializer),
			FieldDeclNode fieldDecl => LookupType(fieldDecl.Initializer),
			_ => null,
		};
	}

	private ThisSymbol GetThisSymbol(ThisExprNode node)
	{
		var type = GetEnclosingScope(node)?.LookupThis() ?? ErrorTypeSymbol.Create();
		// TODO: This will create a lot of symbols when there are errors. Same with other keys.
		// Should create only one for errors? Or somehow do some cleaning on re-analysis.
		return ReasoningContext.ThisSymbolMap.GetOrAdd(new ThisSymbolKey(type), key => new ThisSymbol(key));
	}

	private PtrTypeSymbol GetPtrType(PtrTypeNode node)
	{
		return GetPtrType(node.InnerType == null ? null : LookupSymbol(node.InnerType).AsTypeSymbolOrError());
	}

	private PtrTypeSymbol GetPtrType(TypeSymbol? innerType)
	{
		var key = new PtrTypeKey(innerType);
		return ReasoningContext.PtrTypeMap.GetOrAdd(key, key => new PtrTypeSymbol(key));
	}

	// Supported:
	// - FnDeclNode: From `fn foo(v: i32) i32 { ... }`
	// - MethodDeclNode: ^ but method
	// - TypeNode: e.g. From the type in `var f: fn (i32) i32;`
	// - AliasDeclNode: `alias Delegate as fn (i32) i32;`
	public FnTypeSymbol GetFnType(SyntaxNode node)
	{
		return node switch
		{
			FnDeclNode fnDecl => GetUnnamedFnTypeSymbol(fnDecl.Sig),
			MethodDeclNode methodDecl => GetUnnamedFnTypeSymbol(methodDecl.Sig),
			FnTypeNode fnType => GetUnnamedFnTypeSymbol(fnType),

			// Currently not really used.
			//AliasDeclNode aliasDecl => GetNamedFnTypeSymbol(aliasDecl),

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

	private FnTypeSymbol GetOrCreateUnnamedFnType(FnTypeKey key)
	{
		return ReasoningContext.UnnamedFnTypeMap.GetOrAdd(key, key => new FnTypeSymbol(null, key));
	}

	private FnTypeSymbol GetNamedFnTypeSymbol(AliasDeclNode aliasDecl)
	{
		// TODO: Actually get a named FnTypeSymbol for aliased fn types?
		// Right now this just returns the same shared unnamed symbol as it's created in AliasSymbol's Type (Scope.ResolveType).
		// Also, this method is not even used currently.

		var aliasSymbol = (AliasTypeSymbol)_nodeToSymbolMap[aliasDecl]!;
		return (FnTypeSymbol)aliasSymbol.Type.UnwrapAliases();
	}

	private FnTypeKey CreateFnTypeKey(FnSigNode sig)
	{
		// TODO: Return something else in case of error diags? (p.Type below could be null)
		var isExtern = sig.Attrs.HasAttr(KnownAttrs.Extern);
		var fnSymbol = (FnSymbol)GetDeclaredSymbol(sig.Parent!)!;
		Debug.Assert(fnSymbol != null);
		var receiverType = fnSymbol.MethodReceiverType;
		IEnumerable<TypeSymbol> receiverTypes = receiverType == null ? [] : [receiverType];
		IEnumerable<TypeSymbol> paramTypes = sig.Params.Select(p =>
		{
			return LookupSymbol(p.Type?.Type).AsTypeSymbolOrError();
		});
		return new FnTypeKey(
			isExtern,
			sig.ReturnType == null ? Project.ResolveKnownTypeSymbol(KnownTypeTag.None) : LookupSymbol(sig.ReturnType).AsTypeSymbolOrError(),
			[.. receiverTypes, .. paramTypes]);
	}

	private FnTypeKey CreateFnTypeKey(FnTypeNode fnType)
	{
		var isExtern = fnType.Attrs.HasAttr(KnownAttrs.Extern);
		return new FnTypeKey(
			isExtern,
			fnType.ReturnType == null ? Project.ResolveKnownTypeSymbol(KnownTypeTag.None) : LookupSymbol(fnType.ReturnType).AsTypeSymbolOrError(),
			// TODO: Should be able to handle ParamTypes being null better.
			fnType.ParamTypes == null ? [ErrorTypeSymbol.Create()] : fnType.ParamTypes.Select(p =>
			{
				return LookupSymbol(p.Type).AsTypeSymbolOrError();
			}));
	}

	internal Reasoner? ResolveImportedFileReasoner(ImportNode import)
	{
		if (import.ModulePath is not LiteralExprNode literalExpr || !literalExpr.Literal.TryGetStringValue(out var modulePath) || modulePath == null)
			return null;

		var targetSourceFile = Project.ModuleResolver.ResolveModule(SourceFile, modulePath);
		if (targetSourceFile == null)
			return null;

		return Project.GetReasoner(targetSourceFile);
	}

	internal TypeSymbol ResolveKnownTypeSymbol(KnownTypeTag tag)
	{
		Debug.Assert(_knownTypeTagToTypeSymbolMap != null, "This should only be called on a prelude reasoner.");

		var found = _knownTypeTagToTypeSymbolMap.TryGetValue(tag, out var typeSymbol);
		Debug.Assert(found);

		return typeSymbol!;
	}

	private class Builder(SurabProject project, SyntaxTree tree, bool isPrelude) : SyntaxTreeVisitor
	{
		private class TempTypeContext
		{
			public int FieldCounter = 0;
		}

		private readonly Reasoner _reasoner = new(project, tree);
		private Scope? _parentScope;
		private Symbol? _parentSymbol;
		private TempTypeContext? _typeContext;

		public static Reasoner Build(SurabProject project, SyntaxTree tree, bool isPrelude = false)
		{
			var builder = new Builder(project, tree, isPrelude);
			if (isPrelude)
			{
				builder._reasoner._knownTypeTagToTypeSymbolMap = [];
			}
			builder.Build();
			return builder._reasoner;
		}

		private void Build()
		{
			if (isPrelude)
			{
				// For the prelude, we'll especially go over the top level decls first to fill in BuiltinTypeSystem first thing before doing anything else.
				// Lots of other processes depends on these known types to be created first.

				Debug.Assert(tree.Root is ModuleNode);
				var moduleNode = (ModuleNode)tree.Root;
				foreach (var decl in moduleNode.Decls)
				{
					var symbol = GetOrCreateSymbolOnNode(decl);
					Debug.Assert(symbol != null);

					if (symbol is TypeSymbol typeSymbol)
					{
						var tag = KnownTypes.GetTag(typeSymbol.Name);
						typeSymbol.KnownTypeTag = tag;
						if (tag != null)
						{
							_reasoner._knownTypeTagToTypeSymbolMap![tag.Value] = typeSymbol;
						}
					}
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

			if (scope != null || _parentScope != null)
			{
				_reasoner._nodeToScopeMap[node] = scope ?? _parentScope!;
			}

			var prevParentScope = _parentScope;
			if (scope != null)
			{
				_parentScope = scope;
			}

			var prevParentSymbol = _parentSymbol;
			if (symbol != null)
			{
				_parentSymbol = symbol;
			}
			var prevTypeContext = _typeContext;
			if (typeContext != null)
			{
				_typeContext = typeContext;
			}

			Walk(node);

			if (scope?.ShouldEndScope(node) ?? true)
			{
				// Restoring the parent scope effectively means the scope ends here.
				_parentScope = prevParentScope;
			}

			_parentSymbol = prevParentSymbol;
			_typeContext = prevTypeContext;
		}

		private Symbol? CreateSymbol(SyntaxNode node)
		{
			var symbol = GetOrCreateSymbolOnNode(node);
			symbol?.SetParent(_parentSymbol);
			return symbol;
		}

		private Symbol? GetOrCreateSymbolOnNode(SyntaxNode node)
		{
			if (_reasoner._nodeToSymbolMap.TryGetValue(node, out var symbol))
				return symbol;

			symbol = node switch
			{
				ConstDeclNode n => new ConstSymbol(n),
				FnDeclNode n => new FnSymbol(n.Sig),
				MethodDeclNode n => new FnSymbol(n.Sig),
				FieldDeclNode n => new FieldSymbolImpl(n, _typeContext == null ? -1 : _typeContext.FieldCounter++),
				StructDeclNode n => new StructTypeSymbol(n),
				ClassDeclNode n => new ClassTypeSymbol(n),
				AliasDeclNode n => new AliasTypeSymbol(n),

				VarStmtNode n => new LocalSymbol(n),

				ParamNode n => new ParamSymbol(n),

				_ => null,
			};

			if (symbol != null)
			{
				_reasoner._nodeToSymbolMap[node] = symbol;
			}

			return symbol;
		}

		private Scope? CreateScope(SyntaxNode node, Symbol? symbol)
		{
			var scope = CreateScopeOnNode(node, symbol);
			if (scope != null)
			{
				scope.Reasoner = _reasoner;
				scope.SetParent(_parentScope);
				//_reasoner._nodeToScopeMap[node] = scope;
			}
			return scope;
		}

		private Scope? CreateScopeOnNode(SyntaxNode node, Symbol? symbol)
		{
			return node switch
			{
				// Temporarily give non prelude modules the prelude reasoner to lookup exported symbols.
				// Later, this should be given as part of the imported modules.
				ModuleNode n => CreateModuleScope(n),
				StructDeclNode n => new StructDeclScope(n, (StructTypeSymbol)symbol!),
				ClassDeclNode n => new ClassDeclScope(n, (NamedTypeSymbol)symbol!),
				FnDeclNode n => new FnScope(n.Sig, (FnSymbol)symbol!),
				MethodDeclNode n => new FnScope(n.Sig, (FnSymbol)symbol!),

				// Create a new local scope on each new var that will leave until the end of the enclosing block.
				VarStmtNode => new LocalScope((LocalSymbol)symbol!),
				// TODO: const?

				_ => null,
			};
		}

		private ModuleScope CreateModuleScope(ModuleNode node)
		{
			return new ModuleScope(node, _reasoner, isPrelude ? null : project.PreludeReasoner);
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
