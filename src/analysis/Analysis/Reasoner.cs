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

	private Reasoner(SurabProject project, ModuleSymbol module, SyntaxTree tree)
	{
		Tree = tree;
		Module = module;
		Project = project;
	}

	public SurabProject Project { get; }

	public ModuleSymbol Module { get; }

	public SyntaxTree Tree { get; }

	public ProjectFile File => Tree.File;

	internal SymbolCache SymbolCache => Project.SymbolCache;

	internal static Reasoner Create(SurabProject project, ModuleSymbol module, SyntaxTree tree)
	{
		return Builder.Build(project, module, tree);
	}

	internal static Reasoner CreatePrelude(SurabProject project, ModuleSymbol module, SyntaxTree tree)
	{
		return Builder.Build(project, module, tree, isPrelude: true);
	}

	/// <summary>
	/// Tries getting a <see cref="Symbol"/> that is declared on the given node.
	/// </summary>
	public bool TryGetDeclaredSymbol(SyntaxNode node, [NotNullWhen(true)] out Symbol? symbol)
	{
		return node switch
		{
			DeclStmtNode declStmt => TryGetDeclaredSymbol(declStmt.Decl, out symbol),
			FnSigNode sig when sig.Parent != null => TryGetDeclaredSymbol(sig.Parent, out symbol),
			_ => _nodeToSymbolMap.TryGetValue(node, out symbol),
		};
	}

	/// <summary>
	/// Gets a <see cref="Symbol"/> that is declared on the given node.
	/// Returns null if there's no declared symbol.
	/// </summary>
	public Symbol? GetDeclaredSymbol(SyntaxNode node)
	{
		TryGetDeclaredSymbol(node, out var symbol);
		return symbol;
	}

	public NamedTypeSymbol GetDeclaredSymbol(StructDeclNode node)
	{
		return (NamedTypeSymbol)GetDeclaredSymbol((SyntaxNode)node)!;
	}

	public AliasTypeSymbol GetDeclaredSymbol(AliasDeclNode node)
	{
		return (AliasTypeSymbol)GetDeclaredSymbol((SyntaxNode)node)!;
	}

	public LocalSymbol GetDeclaredSymbol(VarDeclNode node)
	{
		return (LocalSymbol)GetDeclaredSymbol((SyntaxNode)node)!;
	}

	public FnSymbol GetDeclaredSymbol(FnDeclNode node)
	{
		return (FnSymbol)GetDeclaredSymbol((SyntaxNode)node)!;
	}

	public FnSymbol GetDeclaredSymbol(MethodDeclNode node)
	{
		return (FnSymbol)GetDeclaredSymbol((SyntaxNode)node)!;
	}

	public GenericParamTypeSymbol GetDeclaredSymbol(GenericParamNode node)
	{
		return (GenericParamTypeSymbol)GetDeclaredSymbol((SyntaxNode)node)!;
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

	internal FileScope GetFileScope()
	{
		var scope = GetEnclosingScope(Tree.Root);
		Debug.Assert(scope is FileScope);
		return (FileScope)scope;
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
	/// Gets all diags (syntax and semantic).
	/// </summary>
	public IEnumerable<Diag> GetAllDiags()
	{
		// Syntax
		var parserDiags = Tree.Diags.Diags;

		// Semantic
		var semanticDiags = TypeChecker.Analyze(this);

		return [.. parserDiags, .. semanticDiags.Diags];
	}

	/// <summary>
	/// Looks up the <see cref="Symbol"/> that is referred to in the given node.
	/// If a symbol can't be found, `null` is returned.
	/// </summary>
	public Symbol? LookupSymbol(SyntaxNode? node)
	{
		return node switch
		{
			NameExprNode n => LookupSymbol(n),
			NameNode n => LookupSymbol(n),
			ThisExprNode n => LookupSymbol(n),
			PtrTypeNode n => LookupSymbol(n),
			ExprTypeNode n => LookupSymbol(n.Expr),
			FnTypeNode n => LookupSymbol(n),
			ArgNode n => LookupSymbol(n.Expr),
			//CallExprNode n => LookupSymbol(n.Callee),
			AccessExprNode n => LookupSymbol(n),
			ModuleAccessExprNode n => LookupSymbol(n),
			GenericArgNode n => LookupSymbol(n.Type),

			_ => null,
		};
	}

	public Symbol? LookupSymbol(TypeNode? typeNode)
	{
		return typeNode switch
		{
			PtrTypeNode n => LookupSymbol(n),
			ExprTypeNode n => LookupSymbol(n.Expr),
			FnTypeNode n => LookupSymbol(n),
			_ => null,
		};
	}

	public Symbol? LookupSymbol(NameExprNode node)
	{
		if (TryResolveNameExprSymbolInCreationExpr(node, out var creationExprSymbol))
		{
			return creationExprSymbol;
		}
		else
		{
			return LookupSymbol(node.Name);
		}
	}

	public Symbol? LookupSymbol(NameNode node)
	{
		var symbol = LookupSymbol(node.Identifier, node);
		return ResolveSymbolWithName(symbol, node);
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
		// Lookup the field/method/nested/etc symbol in the access expr.

		var targetType = LookupType(node.Target);

		// Allow pass through type analysis on pointers for access exprs, as compiler will do auto derefing.
		// REVIEW: PtrTypeSymbol's GetMember already does pass through, so this isn't really needed?
		targetType = targetType.UnwrapPointers();

		var member = targetType?.GetMember(node.Name.Identifier);
		return ResolveSymbolWithName(member, node.Name);
	}

	private Symbol? LookupSymbol(ModuleAccessExprNode node)
	{
		var targetSymbol = LookupModule(node.Target);
		if (targetSymbol is not ModuleSymbol module)
		{
			return null;
		}

		return module.GetExportedSymbol(node.Name.Identifier.Value);
	}

	// TODO: What does "exported" mean in this context? (right now it means a public symbol in a module)
	/// <summary>
	/// Looks up an exported <see cref="Symbol"/> matching the given name.
	/// </summary>
	public Symbol? LookupExportedSymbolByName(string name)
	{
		var scope = GetFileScope();
		return scope.LookupExportedIdentifier(name);
	}

	public List<Symbol> GetExportedSymbols()
	{
		// TODO: Need to report a conflict for exported symbols in the same module.
		var scope = GetFileScope();
		return [.. scope.Decls];
	}

	/// <summary>
	/// Looks up the <see cref="TypeSymbol"/> that represents the given node.
	/// The result is never null. If type resolving fails then an <see cref="UnknownTypeSymbol"/> is returned.
	/// </summary>
	public TypeSymbol LookupType(SyntaxNode? node)
	{
		return node switch
		{
			BareExprStmtNode n => LookupType(n),
			ExprNode n => LookupType(n),
			ArgNode n => LookupType(n.Expr),
			EqualsExprClauseNode n => LookupType(n.Expr),

			// Should we really support non expr nodes like these in LookupType or should we force the consumer to use the right thing?
			_ when node?.DeclaresSymbol() ?? false => LookupTypeFromDeclaredSymbol(node),

			_ => UnknownTypeSymbol.Create(),
		};
	}

	private TypeSymbol LookupTypeFromDeclaredSymbol(SyntaxNode node)
	{
		var symbol = GetDeclaredSymbol(node) as ISymbolHasType;
		return symbol?.Type ?? UnknownTypeSymbol.Create();
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
			ModuleAccessExprNode n => LookupType(n),
			LiteralExprNode n => LookupType(n),
			NameExprNode n => LookupType(n),
			ThisExprNode n => LookupType(n),
			TypeCreationExprNode n => LookupType(n),

			_ => UnknownTypeSymbol.Create(),
		};
	}

	private TypeSymbol LookupType(BlockExprNode node)
	{
		var lastStmt = node.Stmts.LastOrDefault();
		if (lastStmt == null)
		{
			return UnknownTypeSymbol.Create();
		}

		return LookupType(lastStmt);
	}

	private TypeSymbol LookupType(UnaryExprNode node)
	{
		var exprType = LookupType(node.Expr);
		return node.Op.UnaryOperatorTokenKind switch
		{
			UnaryOperatorTokenKind.Deref => exprType is PtrTypeSymbol ptrType ? ptrType.InnerType.AsTypeSymbolOrUnknown() : exprType,
			UnaryOperatorTokenKind.Address => GetPtrType(exprType),
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
		return fnType?.ReturnType ?? UnknownTypeSymbol.Create();
	}

	private TypeSymbol LookupType(AccessExprNode node)
	{
		var memberSymbol = LookupSymbol(node);
		return GetTypeOfSymbol(memberSymbol, this.IsInstanceAccess(node));
	}

	private TypeSymbol LookupType(ModuleAccessExprNode node)
	{
		var symbol = LookupSymbol(node);
		return GetTypeOfSymbol(symbol, false);
	}

	private TypeSymbol GetTypeOfSymbol(Symbol? symbol, bool isInstanceAccess)
	{
		return symbol switch
		{
			// For `a.b()`, when it's an instance access, we want to return an fn type without the receiver.
			// TypeWithoutReceiver accounts for both fns and methods, so we can use it here without a method check.
			FnSymbol fn => isInstanceAccess ? fn.TypeWithoutReceiver : fn.Type,
			TypeSymbol type => type,
			ISymbolHasType hasType => hasType.Type,
			_ => UnknownTypeSymbol.Create(),
		};
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

	private TypeSymbol LookupType(NameExprNode node)
	{
		return LookupSymbol(node) switch
		{
			TypeSymbol type => type,
			ISymbolHasType hasType => hasType.Type,
			_ => UnknownTypeSymbol.Create(),
		};
	}

	private TypeSymbol LookupType(ThisExprNode node)
	{
		var thisSymbol = LookupSymbol(node) as ThisSymbol;
		return thisSymbol?.Type ?? UnknownTypeSymbol.Create();
	}

	private TypeSymbol LookupType(TypeCreationExprNode node)
	{
		var targetType = node.TypeExpr switch
		{
			// A { ... }
			NameExprNode nameExpr => LookupSymbol(nameExpr.Name),
			// A.B { ... }
			AccessExprNode accessExpr => LookupSymbol(accessExpr),
			// a::B { ... }
			ModuleAccessExprNode moduleAccessExpr => LookupSymbol(moduleAccessExpr),
			_ => null,
		};

		if (targetType != null && targetType.HasGenericParams() && !targetType.HasGenericArgs())
		{
			// Not closed.
			// TODO: Try infer generic args from node.MapInitializer.
		}

		return targetType.AsTypeSymbolOrUnknown();
	}

	public (FnSymbol? Fn, FnTypeSymbol? FnType) LookupFnSymbolOrType(CallExprNode callExpr)
	{
		if (LookupSymbol(callExpr.Callee) is FnSymbol fn)
		{
			return (fn, fn.Type);
		}

		return (null, LookupType(callExpr.Callee) as FnTypeSymbol);
	}

	public ModuleSymbol? LookupModule(SyntaxNode? node)
	{
		return node switch
		{
			NameExprNode n => LookupModule(n),
			AccessExprNode n => LookupModule(n),

			_ => null,
		};
	}

	private ModuleSymbol? LookupModule(NameExprNode nameExpr)
	{
		return GetFileScope().LookupImportedModule(nameExpr.Name.Identifier.Value);
	}

	private ModuleSymbol? LookupModule(AccessExprNode accessExpr)
	{
		var path = GetModuleImportPathFromExpr(accessExpr);
		if (path == null) return null;
		return Project.ResolveModule(this, path);
	}

	/// <summary>
	/// Resolves a type, binding the type node to its type symbol.
	/// This method never returns `null`. If resolving fails a special <see cref="UnknownTypeSymbol"/> will be returned.
	/// </summary>
	public ResolvedType ResolveType(SyntaxNode node, TypeNode? typeNode)
	{
		if (typeNode != null)
		{
			return ResolveType(typeNode);
		}
		else
		{
			var boundType = InferTypeOfDeclaringNode(node);
			return ResolvedType.Inferred(boundType ?? UnknownTypeSymbol.Create());
		}
	}

	public ResolvedType ResolveType(SyntaxNode node, ColonTypeNode? colonType) => ResolveType(node, colonType?.Type);

	/// <summary>
	/// Resolves a type, binding the type node to its type symbol.
	/// This method never returns `null`. If resolving fails a special <see cref="UnknownTypeSymbol"/> will be returned.
	/// </summary>
	public ResolvedType ResolveType(TypeNode typeNode)
	{
		var resolvedType = typeNode switch
		{
			PtrTypeNode ptrTypeNode => LookupSymbol(ptrTypeNode),
			ExprTypeNode exprTypeNode => LookupSymbol(exprTypeNode),
			FnTypeNode fnTypeNode => LookupSymbol(fnTypeNode),
			MissingTypeNode => UnknownTypeSymbol.Create(),
			_ => throw new UnreachableException(),
		};
		return ResolvedType.Typed(resolvedType.AsTypeSymbolOrUnknown());
	}

	/// <summary>
	/// If the identifier expr is a part of setting a symbol in a creation expr, this method returns true and tries to resolve that symbol.
	/// Otherwise false is returned.
	/// </summary>
	private bool TryResolveNameExprSymbolInCreationExpr(NameExprNode nameExpr, out Symbol? symbol)
	{
		symbol = null;
		// The identifier inside a creation expr: `S1 { a = 42 }`
		if (nameExpr.Parent is AssignmentExprNode assignmentExpr &&
			assignmentExpr.Lhs == nameExpr &&
			assignmentExpr.Parent is MapInitializerItemNode mapInitializerItem &&
			mapInitializerItem.Parent is MapInitializerNode mapInitializer &&
			mapInitializer.Parent is TypeCreationExprNode typeCreationExpr)
		{
			var type = LookupType(typeCreationExpr);
			Debug.Assert(type is NamedTypeSymbol);
			symbol = ((NamedTypeSymbol)type).GetCreationMemberSymbol(nameExpr.Name.Identifier)?.Symbol;
			return true;
		}
		return false;
	}

	public TypeSymbol? InferTypeOfDeclaringNode(SyntaxNode node)
	{
		return node switch
		{
			VarDeclNode varDecl => LookupType(varDecl.Initializer),
			FieldDeclNode fieldDecl => LookupType(fieldDecl.Initializer),
			_ => null,
		};
	}

	private ThisSymbol GetThisSymbol(ThisExprNode node)
	{
		// TODO: This will create a lot of symbols when there are errors. Same with other keys.
		// Should create only one for errors? Or somehow do some cleaning on re-analysis.

		//var thisSymbol = (ThisSymbol)LookupSymbol(node);
		//thisSymbol.Type

		var type = GetEnclosingScope(node)?.LookupThis() ?? UnknownTypeSymbol.Create();
		// TODO: Account for @receiver_by_val.
		type = SymbolCache.GetPtrType(type);
		//if (type is StructTypeSymbol) { create ptr }
		return SymbolCache.GetThis(type);
	}

	internal PtrTypeSymbol GetPtrType(PtrTypeNode node)
	{
		var type = node.InnerType == null ? null : LookupSymbol(node.InnerType).AsTypeSymbolOrUnknown();
		return SymbolCache.GetPtrType(type);
	}

	internal PtrTypeSymbol GetPtrType(TypeSymbol type)
	{
		return SymbolCache.GetPtrType(type);
	}

	// Supported:
	// - FnDeclNode: From `fn foo(v: i32) i32 { ... }`
	// - MethodDeclNode: ^ but method
	// - TypeNode: e.g. From the type in `var f: fn (i32) i32;`
	// - AliasDeclNode: `alias fn (i32) i32 as Delegate;`
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
		return SymbolCache.GetFnType(key);
	}

	private FnTypeSymbol GetUnnamedFnTypeSymbol(FnTypeNode fnType)
	{
		var key = CreateFnTypeKey(fnType);
		return SymbolCache.GetFnType(key);
	}

	private FnTypeSymbol GetNamedFnTypeSymbol(AliasDeclNode aliasDecl)
	{
		// TODO: Actually get a named FnTypeSymbol for aliased fn types?
		// Right now this just returns the same shared unnamed symbol as it's created in AliasSymbol's Type (Scope.ResolveType).
		// Also, this method is not even used currently.

		var aliasSymbol = (AliasTypeSymbol)_nodeToSymbolMap[aliasDecl]!;
		return (FnTypeSymbol)aliasSymbol.TargetType.UnwrapTypeAliases();
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
			return LookupSymbol(p.Type?.Type).AsTypeSymbolOrUnknown();
		});
		return new FnTypeKey(
			isExtern,
			sig.ReturnType == null ? Project.ResolveKnownTypeSymbol(KnownTypeTag.None) : LookupSymbol(sig.ReturnType).AsTypeSymbolOrUnknown(),
			[.. receiverTypes, .. paramTypes]);
	}

	private FnTypeKey CreateFnTypeKey(FnTypeNode fnType)
	{
		var isExtern = fnType.Attrs.HasAttr(KnownAttrs.Extern);
		return new FnTypeKey(
			isExtern,
			fnType.ReturnType == null ? Project.ResolveKnownTypeSymbol(KnownTypeTag.None) : LookupSymbol(fnType.ReturnType).AsTypeSymbolOrUnknown(),
			// TODO: Should be able to handle ParamTypes being null better.
			fnType.ParamTypes == null ? [UnknownTypeSymbol.Create()] : fnType.ParamTypes.Select(p =>
			{
				return LookupSymbol(p.Type).AsTypeSymbolOrUnknown();
			}));
	}

	[return: NotNullIfNotNull(nameof(symbol))]
	private Symbol? ResolveSymbolWithName(Symbol? symbol, NameNode name)
	{
		return symbol switch
		{
			null => null,
			_ => CloseGeneric(symbol, name),
		};
	}

	// REVIEW: TryCloseGeneric?
	public Symbol CloseGeneric(Symbol symbol, IReadOnlyList<TypeSymbol> genericArgs)
	{
		return symbol switch
		{
			IGenericSymbol genericSymbol when genericArgs.Any() => SymbolCache.CloseGeneric(symbol, genericSymbol.GenericParams, genericArgs),
			_ when genericArgs.Any() => CloseInvalidSymbol(symbol),
			_ => symbol,
		};

		Symbol CloseInvalidSymbol(Symbol symbol)
		{
			return UnknownTypeSymbol.CreateInvalidClosedGeneric(symbol, genericArgs);
		}
	}

	private Symbol CloseGeneric(Symbol symbol, NameNode name)
	{
		var genericArgs = ResolveGenericArgsSymbolsFromName(name);
		return CloseGeneric(symbol, genericArgs);
	}

	private List<TypeSymbol> ResolveGenericArgsSymbolsFromName(NameNode name)
	{
		return name.GenericArgsList?.GenericArgs
			.Select(a => LookupSymbol(a).AsTypeSymbolOrUnknown())
			.ToList() ?? [];
	}

	internal ModuleSymbol ResolveCurrentModule()
	{
		// TODO: Cache?
		return Project.ResolveModule(this);
	}

	internal ResolvedImport ResolveImport(ImportNode import)
	{
		var path = GetModuleImportPathFromExpr(import.Target);
		if (path == null) return ResolvedImport.Failed();

		var targetModule = Project.ResolveModule(this, path);
		if (targetModule == null) return ResolvedImport.Failed();

		if (import.AsIdentifier != null)
		{
			var alias = import.AsIdentifier.Name.Identifier.Value;
			targetModule = new AliasedModuleSymbol(targetModule, alias);
			return ResolvedImport.Resolved(targetModule, [targetModule]);
		}

		return ResolvedImport.Resolved(targetModule, [targetModule]);
	}

	/// <summary>
	/// Returns a string array representing the expr: std.memory => ["std", "memory"]
	/// </summary>
	private IEnumerable<string>? GetModuleImportPathFromExpr(ExprNode? expr)
	{
		var reversePaths = new List<string>();

		var current = expr;
		while (true)
		{
			if (current is NameExprNode nameExpr)
			{
				reversePaths.Add(nameExpr.Name.Identifier.Value);
				break;
			}
			else if (current is AccessExprNode accessExpr)
			{
				reversePaths.Add(accessExpr.Name.Identifier.Value);
				current = accessExpr.Target;
			}
			else
			{
				return null;
			}
		}

		reversePaths.Reverse();
		return reversePaths;
	}

	/// <summary>
	/// This is only to be called on the prelude reasoner. Use <see cref="SurabProject.ResolveKnownTypeSymbol(KnownTypeTag)"/> instead.
	/// </summary>
	internal TypeSymbol ResolveKnownTypeSymbol(KnownTypeTag tag)
	{
		Debug.Assert(_knownTypeTagToTypeSymbolMap != null, "This should only be called on a prelude reasoner.");

		var found = _knownTypeTagToTypeSymbolMap.TryGetValue(tag, out var typeSymbol);
		Debug.Assert(found);

		return typeSymbol!;
	}

	private class Builder(SurabProject project, ModuleSymbol module, SyntaxTree tree, bool isPrelude) : SyntaxTreeVisitor
	{
		private class TempTypeContext
		{
			public int FieldCounter = 0;
			public int GenericParamCounter = 0;
		}

		private readonly Reasoner _reasoner = new(project, module, tree);
		private Scope? _parentScope;
		private Symbol? _parentSymbol;
		private TempTypeContext? _typeContext;

		public static Reasoner Build(SurabProject project, ModuleSymbol module, SyntaxTree tree, bool isPrelude = false)
		{
			var builder = new Builder(project, module, tree, isPrelude);
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
				// UPDATE: This whole block doesn't seem to be necessary. Instead, we can just add the KnownTypeTag assignment thing inside GetOrCreateSymbolOnNode.

				// For the prelude, we'll especially go over the top level decls first to fill in the known type tag map first thing before doing anything else.
				// Some places depend on these known types to be created first.

				Debug.Assert(tree.Root is FileNode);
				var fileNode = (FileNode)tree.Root;
				foreach (var decl in fileNode.Decls)
				{
					var symbol = GetOrCreateSymbolOnNode(decl);
					Debug.Assert(symbol != null);

					if (symbol is TypeSymbol typeSymbol)
					{
						var tag = KnownTypes.GetTag(typeSymbol.Identifier);
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
			// This (symbol == _parentSymbol) is possible for DeclStmtNode since we return the symbol on the parent even though it's defined on the child.
			// Not doing this check means we'll set S as the parent of S which can cause a stack overflow in analysis.
			// REVIEW: Find a better way to handle this?
			if (symbol != _parentSymbol)
			{
				var parent = _parentSymbol ?? _reasoner.Module;
				Debug.Assert(parent != null); // Will be either the module or an actual parent symbol.
				symbol?.SetParent(parent);
			}
			return symbol;
		}

		private Symbol? GetOrCreateSymbolOnNode(SyntaxNode node)
		{
			if (_reasoner._nodeToSymbolMap.TryGetValue(node, out var symbol))
				return symbol;

			symbol = node switch
			{
				// This is very important to have here so that the decl stmt can get the symbol that gets provided
				// to the scope creation below. (i.e. We need the LocalSymbol for the scope created on VarDeclNode when the current node is DeclStmtNode)
				DeclStmtNode n => GetOrCreateSymbolOnNode(n.Decl),

				ConstDeclNode n => new SrcConstSymbol(n),
				VarDeclNode n => new SrcLocalSymbol(n),
				FnDeclNode n => new SrcFnSymbol(n.Sig),
				MethodDeclNode n => new SrcFnSymbol(n.Sig),
				FieldDeclNode n => new SrcFieldSymbol(n, _typeContext == null ? throw new UnreachableException() : _typeContext.FieldCounter++),
				StructDeclNode n => new SrcNamedTypeSymbol(n),
				AliasDeclNode n => new SrcAliasTypeSymbol(n),
				GenericParamNode n => new SrcGenericParamTypeSymbol(n, _typeContext == null ? throw new UnreachableException() : _typeContext.GenericParamCounter++),

				ParamNode n => new SrcParamSymbol(n),

				_ => null,
			};

			if (symbol != null)
			{
				_reasoner._nodeToSymbolMap[node] = symbol;
				symbol.Reasoner = _reasoner;
				symbol.SymbolCache = _reasoner.SymbolCache;
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
				//////_reasoner._nodeToScopeMap[node] = scope;
			}
			return scope;
		}

		private Scope? CreateScopeOnNode(SyntaxNode node, Symbol? symbol)
		{
			return node switch
			{
				FileNode n => CreateFileScope(n),

				// This is very important to have here so that the decls inside can create a scope on this level.
				// If we wait until we go deeper, we'll create scopes with smaller lifetimes.
				DeclStmtNode n => CreateScopeOnNode(n.Decl, symbol),

				// TODO: const?

				// Create a new local scope on each new var that will live until the end of the enclosing block.
				VarDeclNode => new LocalScope((LocalSymbol)symbol!),
				StructDeclNode => new NamedTypeDeclScope((NamedTypeSymbol)symbol!),
				AliasDeclNode => new AliasTypeDeclScope((AliasTypeSymbol)symbol!),
				FnDeclNode => new FnScope((FnSymbol)symbol!),
				MethodDeclNode => new FnScope((FnSymbol)symbol!),
				BlockExprNode n => new BlockScope(n),

				_ => null,
			};
		}

		private FileScope CreateFileScope(FileNode node)
		{
			return new FileScope(node, _reasoner, isPrelude ? null : project.PreludeReasoner);
		}

		private static TempTypeContext? CreateTypeContext(SyntaxNode node)
		{
			return node switch
			{
				StructDeclNode or ClassDeclNode => new TempTypeContext(),
				GenericParamsListNode => new TempTypeContext(),

				_ => null,
			};
		}
	}
}

public static class ReasonerExtensions
{
	/// <summary>
	/// Returns whether the access expr does an instance access (a.b), as opposed to static access (A.b).
	/// </summary>
	public static bool IsInstanceAccess(this Reasoner reasoner, AccessExprNode accessExpr)
	{
		var targetSymbol = reasoner.LookupSymbol(accessExpr.Target);
		return !targetSymbol.IsType();
	}
}
