using System.Diagnostics;
using Surab.Analysis;

namespace Surab.Compilation;

internal class LoweredProject(IReadOnlyList<LoweredTree> trees)
{
	public IReadOnlyList<LoweredTree> Trees { get; } = trees;
}

/// <summary>
/// Lowers from a <see cref="SyntaxTree"/> to a <see cref="LoweredTree"/>.
///
/// This process simplifies nodes, hoists nested declarations, makes implicit generic types explicit,
/// desugars syntax, binds symbols and resolves types.
/// </summary>
internal class Lowerer
{
	private readonly Reasoner _reasoner;

	private readonly HoistingBag<LoweredDecl> _declsHoistingBag = new();
	private HoistingBag<LoweredVariableDeclareStmt> _varStmtsHoistingBag = new();
	private bool _insideDecl;

	private Lowerer(Reasoner reasoner)
	{
		_reasoner = reasoner;
	}

	internal static LoweredProject LowerProject(SurabProject project)
	{
		var loweredTrees = project.GetAllReasoners()
			.Select(Lower)
			.ToList();
		return new LoweredProject(loweredTrees);
	}

	internal static LoweredTree Lower(Reasoner reasoner)
	{
		var lowerer = new Lowerer(reasoner);
		var tree = lowerer.Lower();
		lowerer.AssertHoistingBagsAreEmpty();
		return tree;
	}

	private LoweredTree Lower()
	{
		var root = Lower(_reasoner.Tree.Root)!;
		return new LoweredTree(root);
	}

	private LoweredNode Lower(SyntaxNode node)
	{
		var result = node switch
		{
			FileNode n => Lower(n),
			//ConstDeclNode n => Lower(n),
			VarDeclNode n => Lower(n),
			FnDeclNode n => Lower(n),
			MethodDeclNode n => Lower(n),
			StructDeclNode n => Lower(n),
			//ClassDeclNode n => Lower(n),
			AliasDeclNode => LoweredElidedDecl.Instance,
			ReturnStmtNode n => Lower(n),
			DeclStmtNode n => Lower(n),
			ExprStmtNode n => Lower(n),
			GroupingExprNode n => Lower(n.Expr),
			BareExprStmtNode n => Lower(n),
			BlockExprNode n => Lower(n),
			AssignmentExprNode n => Lower(n),
			UnaryExprNode n => Lower(n),
			BinaryExprNode n => Lower(n),
			CallExprNode n => Lower(n),
			AccessExprNode n => Lower(n),
			ModuleAccessExprNode n => Lower(n),
			LiteralExprNode n => Lower(n),
			NameExprNode n => Lower(n),
			ThisExprNode n => Lower(n),
			TypeCreationExprNode n => Lower(n),

			// All other nodes not in this switch should be taken care of as part of these nodes mentioned above.
			_ => throw new UnreachableException(),
		};

		return result;
	}

	private LoweredNode Lower(FileNode node)
	{
		return new LoweredFile(
			node,
			LowerWithCast(node.Decls));
	}

	private LoweredNode Lower(VarDeclNode node)
	{
		var symbol = _reasoner.GetDeclaredSymbol(node);
		Debug.Assert(symbol != null);

		var variableStmt = new LoweredVariableDeclareStmt(
			node,
			symbol);
		_varStmtsHoistingBag.Hoist(variableStmt);

		if (node.Initializer == null)
		{
			return LoweredElidedStmt.Instance;
		}

		return new LoweredVariableStoreStmt(
			node,
			symbol,
			LowerWithCast(node.Initializer.Expr));
	}

	private LoweredNode Lower(FnDeclNode node)
	{
		return HoistLocalDecls((node) =>
		{
			var fnSymbol = _reasoner.GetDeclaredSymbol(node);
			Debug.Assert(fnSymbol != null);
			var body = node.Body == null ? null : LowerFnBlock(node.Body);

			return new LoweredFnDecl(
				node,
				fnSymbol,
				body);
		}, node);
	}

	private LoweredNode Lower(MethodDeclNode node)
	{
		return HoistLocalDecls((node) =>
		{
			var fnSymbol = _reasoner.GetDeclaredSymbol(node);
			Debug.Assert(fnSymbol != null);
			var body = node.Body == null ? null : LowerFnBlock(node.Body);

			return new LoweredFnDecl(
				node,
				fnSymbol,
				body);
		}, node);
	}

	private LoweredNode Lower(StructDeclNode node)
	{
		return HoistLocalDecls((node) =>
		{
			var namedTypeSymbol = _reasoner.GetDeclaredSymbol(node);

			var fieldDecls = namedTypeSymbol.Members.OfType<FieldSymbol>();
			var nonFieldDecls = namedTypeSymbol.Members.Where(x => x.Kind is not SymbolKind.Field);

			foreach (var nonFieldDecl in nonFieldDecls)
			{
				// Non field decls should always be hoisted and so the lowering result should be elided.
				Debug.Assert(nonFieldDecl.DeclaringNode != null);
				var result = Lower(nonFieldDecl.DeclaringNode);
				Debug.Assert(result == LoweredElidedDecl.Instance);
			}

			return new LoweredTypeDecl(
				node,
				namedTypeSymbol,
				fieldDecls);
		}, node);
	}

	private LoweredNode Lower(ReturnStmtNode node)
	{
		var expr = node.Expr == null ? null : LowerWithCast(node.Expr);
		return new LoweredReturnStmt(
			node,
			expr);
	}

	private LoweredStmt Lower(DeclStmtNode node)
	{
		return Lower(node.Decl) switch
		{
			// This happens with decls that are lowered to stmts such as a var decl
			// (we'll get either a var store, or an elided stmt since var declare stmt itself will be hoisted).
			LoweredStmt stmt => stmt,

			// This happens when lowering a nested decl inside a fn for example.
			// This method should always return a stmt, so we simply return an elided stmt instead of the elided decl.
			LoweredDecl decl when decl == LoweredElidedDecl.Instance => LoweredElidedStmt.Instance,

			_ => throw new UnreachableException(),
		};
	}

	private LoweredNode Lower(ExprStmtNode node)
	{
		// Result might not be an expr (e.g. `a = 2;` it'll be var store stmt), in which case return as is
		// as we won't need to wrap in an LoweredExprStmt in this case.
		var result = Lower(node.Expr);
		if (result is LoweredExpr expr)
		{
			return new LoweredExprStmt(
				node,
				expr);
		}
		else
		{
			return result;
		}
	}

	private LoweredNode Lower(BareExprStmtNode node)
	{
		// This is called in cases such as standalone blocks.
		return Lower(node.Expr);
	}

	/// <summary>
	/// Lowers the body of a function or a method.
	/// </summary>
	private LoweredStmtList LowerFnBlock(BlockExprNode node)
	{
		Debug.Assert(node.Parent is FnDeclNode or MethodDeclNode);

		// Store and restore previous hoisting bag. Imagine a function inside another function.
		var prevBag = _varStmtsHoistingBag;
		_varStmtsHoistingBag = new();

		var (stmts, bareReturnExpr) = LowerBlock(node);

		if (bareReturnExpr != null)
		{
			stmts.Add(new LoweredReturnStmt(
				node,
				bareReturnExpr));
		}

		var hoistedVars = _varStmtsHoistingBag.TakeHoisted();
		_varStmtsHoistingBag = prevBag;

		return new LoweredStmtList(
			node,
			[..hoistedVars, ..stmts]);
	}

	// BareReturnExpr will only be set if there's a bare block return expr (that's not treated as a stmt).
	private (List<LoweredStmt> Stmts, LoweredExpr? BareReturnExpr) LowerBlock(BlockExprNode node)
	{
		var parsedStmts = node.Stmts.ToList();
		var lastParsedStmt = parsedStmts.LastOrDefault();

		var bareReturnExpr = default(LoweredExpr?);
		if (lastParsedStmt is BareExprStmtNode bareExprStmtNode &&
			!bareExprStmtNode.IsTreatedAsStmt)
		{
			parsedStmts.Remove(lastParsedStmt);
			bareReturnExpr = LowerWithCast(bareExprStmtNode.Expr);
		}

		var stmts = LowerWithCast(parsedStmts).ToList();

		return (stmts, bareReturnExpr);
	}

	private LoweredNode Lower(BlockExprNode node)
	{
		var (stmts, bareExpr) = LowerBlock(node);

		if (bareExpr != null)
		{
			return new LoweredStmtListEndingWithExpr(
				node,
				stmts,
				bareExpr);
		}
		else
		{
			return new LoweredStmtList(
				node,
				stmts);
		}
	}

	private LoweredNode Lower(AssignmentExprNode node)
	{
		var symbol = _reasoner.LookupSymbol(node.Lhs);
		if (symbol is LocalSymbol or ParamSymbol)
		{
			var storeStmt = new LoweredVariableStoreStmt(
				node,
				symbol,
				LowerWithCast(node.Rhs));

			var loadExpr = new LoweredVariableLoadExpr(
				node,
				((ISymbolHasType)symbol).Type,
				symbol);

			return new LoweredStmtListEndingWithExpr(
				node,
				[storeStmt],
				loadExpr);
		}
		else if (symbol is FieldSymbol fieldSymbol)
		{
			var accessExpr = (AccessExprNode)node.Lhs; // REVIEW: Is this always valid?
			var loweredTarget = AutoDeref(accessExpr.Target, fieldSymbol.EnclosingType);

			var storeStmt = new LoweredFieldStoreStmt(
				node,
				loweredTarget,
				fieldSymbol,
				LowerWithCast(node.Rhs));

			var loadExpr = new LoweredFieldLoadExpr(
				node,
				fieldSymbol.Type,
				loweredTarget,
				fieldSymbol);

			return new LoweredStmtListEndingWithExpr(
				node,
				[storeStmt],
				loadExpr);
		}
		else
		{
			// ?
			throw new NotImplementedException();
		}
	}

	private LoweredExpr Lower(AccessExprNode node)
	{
		switch (_reasoner.LookupSymbol(node))
		{
			case FieldSymbol field:
				{
					var loweredTarget = AutoDeref(node.Target, field.EnclosingType);
					return new LoweredFieldLoadExpr(
						node,
						field.Type,
						loweredTarget,
						field);
				}

			case FnSymbol fn:
				{
					// The reason this doesn't require a bespoke lowered node is because, unlike fields, methods become simple symbols untied to any struct.
					return new LoweredSymbolLoadExpr(
						node,
						fn.Type,
						fn);
				}

			default:
				throw new UnreachableException();
		};
	}

	private LoweredExpr Lower(ModuleAccessExprNode node)
	{
		var symbol = _reasoner.LookupSymbol(node);
		// Should only be here if we're loading a symbol with a type.
		Debug.Assert(symbol is ISymbolHasType);
		return new LoweredSymbolLoadExpr(
			node,
			((ISymbolHasType)symbol).Type,
			symbol);
	}

	private LoweredNode Lower(UnaryExprNode node)
	{
		var expr = LowerWithCast(node.Expr);

		if (node.Op.UnaryOperatorTokenKind == UnaryOperatorTokenKind.Deref &&
			expr is LoweredUnaryExpr loweredExpr && loweredExpr.Op == UnaryOperatorTokenKind.Address)
		{
			// Deref(Address(expr)) cancel each other, so return the Address's inner expr. (`*&a` => `a`)
			return loweredExpr.Expr;
		}

		return new LoweredUnaryExpr(
			node,
			_reasoner.LookupType(node),
			expr,
			node.Op.UnaryOperatorTokenKind);
	}

	private LoweredNode Lower(BinaryExprNode node)
	{
		return new LoweredBinaryExpr(
			node,
			_reasoner.LookupType(node),
			LowerWithCast(node.Left),
			LowerWithCast(node.Right));
	}

	private LoweredNode Lower(CallExprNode node)
	{
		var calleeExpr = LowerWithCast(node.Callee);

		// symbol will be ParamSymbol for fn ptrs in params for example.
		var symbol = _reasoner.LookupSymbol(node.Callee);

		// Note: LookupType will give as an FnType that doesn't include the receiver param for non static methods accessed through an instance.
		// (see `FnSymbol.TypeWithoutReceiver`)
		// For this reason, we get back the normal type back in these cases below.
		var fnType = (FnTypeSymbol)_reasoner.LookupType(node.Callee).UnwrapTypeAliases();

		var receiverArgs = Enumerable.Empty<LoweredExpr>();
		if (symbol is FnSymbol fnSymbol && node.Callee is AccessExprNode accessExpr && _reasoner.IsInstanceAccess(accessExpr))
		{
			Debug.Assert(fnSymbol.MethodReceiverType != null);
			fnType = fnSymbol.Type; // Set the type to the original type with the receiver param.

			// `a.b()`: We have to do some auto derefing/addressing of `a`.
			var loweredTarget = AutoDeref(accessExpr.Target, fnSymbol.MethodReceiverType);

			// TODO: Account for @receiver_by_val.
			loweredTarget = AutoAddress(node, loweredTarget, fnSymbol.MethodReceiverType);
			receiverArgs = [loweredTarget];
		}

		return new LoweredCallExpr(
			node,
			fnType,
			calleeExpr,
			// No auto deref for args. Might be better this way. (If we want it changes will be required in IsAssignableFrom/TypeChecker)
			[.. receiverArgs, .. LowerWithCast(node.Args.Select(arg => arg.Expr))]);
	}

	private LoweredExpr AutoAddress(SyntaxNode node, LoweredExpr loweredExpr, TypeSymbol targetType)
	{
		// Can only auto address once, unlike auto deref.

		if (targetType.IsAssignableFrom(loweredExpr.Type))
		{
			return loweredExpr;
		}

		return new LoweredUnaryExpr(node, _reasoner.GetPtrType(loweredExpr.Type), loweredExpr, UnaryOperatorTokenKind.Address);
	}

	private LoweredExpr AutoDeref(ExprNode expr, TypeSymbol targetType)
	{
		// TODO: If Type becomes allowed in expr (S.a), this will need an update here.

		var loweredExpr = LowerWithCast(expr);
		var loweredExprType = loweredExpr.Type;

		var currentType = loweredExprType;
		var derefsNeeded = 0;
		while (currentType is PtrTypeSymbol ptrType && !targetType.IsAssignableFrom(currentType))
		{
			derefsNeeded++;
			currentType = ptrType.InnerType;
		}

		if (derefsNeeded == 0)
		{
			return loweredExpr;
		}

		currentType = loweredExprType;
		var currentExpr = loweredExpr;
		for (var i = 0; i < derefsNeeded; i++)
		{
			// currentType can only be a PtrTypeSymbol here.
			// Suppression: Won't be an opaque ptr.
			var newDerefExprType = ((PtrTypeSymbol)currentType).InnerType!;
			var newDerefExpr = new LoweredUnaryExpr(expr, newDerefExprType, currentExpr, UnaryOperatorTokenKind.Deref);

			currentType = newDerefExprType;
			currentExpr = newDerefExpr;
		}

		return currentExpr;
	}

	private LoweredNode Lower(LiteralExprNode node)
	{
		var type = _reasoner.LookupType(node);
		var data = LiteralData.FromLiteralToken(node.Literal);
		return new LoweredLiteralExpr(
			node,
			type,
			data);
	}

	private LoweredNode Lower(NameExprNode node)
	{
		// Should only be here if we're loading a variable?
		var symbol = _reasoner.LookupSymbol(node);
		Debug.Assert(symbol is ISymbolHasType);
		var symbolHasType = (ISymbolHasType)symbol;
		return new LoweredVariableLoadExpr(
			node,
			symbolHasType.Type,
			symbol);
	}

	private LoweredNode Lower(ThisExprNode node)
	{
		Debug.Assert(
			!(node.Parent is EqualsExprClauseNode equalsExprClause && equalsExprClause.Expr == node),
			"Should only be here when loading `this` directly as an rvalue.");

		var type = _reasoner.LookupType(node);
		return new LoweredThisLoadExpr(
			node,
			type);
	}

	private LoweredNode Lower(TypeCreationExprNode node)
	{
		// Lowered into LoweredStmtListEndingWithExpr with:
		// - ObjectAllocStmt
		// - FieldStoreStmt[]
		// - CallExpr (init)
		// - Load back object expr

		var stmts = new List<LoweredStmt>();

		var namedType = (NamedTypeSymbol)_reasoner.LookupType(node).UnwrapTypeAliases();
		var structTempSymbol = new TempSymbol(namedType);

		var structLoadExpr = new LoweredSymbolLoadExpr(node, namedType, structTempSymbol);

		// Object alloc

		var allocStmt = new LoweredObjectAllocStmt(
			node,
			namedType,
			structTempSymbol);
		stmts.Add(allocStmt);

		var initializerItems = node.MapInitializer.WellFormedExprs.Select(expr =>
		{
			var targetSymbol = _reasoner.LookupSymbol(expr.Lhs)!;
			return (targetSymbol, rhs: LowerWithCast(expr.Rhs));
		});

		var initFieldItems = initializerItems.Where(x => x.targetSymbol is FieldSymbol);
		var initParamItems = initializerItems.Where(x => x.targetSymbol is ParamSymbol);

		// Field stores

		foreach (var fieldSymbol in namedType.Members.Fields())
		{
			var value = initFieldItems.FirstOrDefault(initF => initF.targetSymbol == fieldSymbol).rhs;
			value ??= LowerWithCast(fieldSymbol.InitializerExprNode!);
			stmts.Add(new LoweredFieldStoreStmt(node, structLoadExpr, fieldSymbol, value));
		}

		// Call init

		if (namedType.InitializerMethod != null)
		{
			var initializerFnSymbol = namedType.InitializerMethod;
			IEnumerable<LoweredExpr> argsSet = initializerFnSymbol.Params.Select(p =>
			{
				var arg = initParamItems.FirstOrDefault(initP => initP.targetSymbol == p).rhs;
				arg ??= LowerWithCast(p.InitializerExprNode!);
				return arg;
			});
			// TODO: Account for @receiver_by_val.
			var receiver = AutoAddress(node, structLoadExpr, initializerFnSymbol.MethodReceiverType!);
			var callInitExpr = new LoweredCallExpr(
				node,
				initializerFnSymbol.Type,
				new LoweredSymbolLoadExpr(node, initializerFnSymbol.Type, initializerFnSymbol),
				[receiver, .. argsSet]);
			stmts.Add(new LoweredExprStmt(node, callInitExpr));
		}

		// Combine

		return new LoweredStmtListEndingWithExpr(
			node,
			stmts,
			structLoadExpr);
	}

	private LoweredDecl LowerWithCast(DeclNode node) => (LoweredDecl)Lower(node);

	private IEnumerable<LoweredDecl> LowerWithCast(IEnumerable<DeclNode> nodes)
	{
		return nodes.Select(LowerWithCast);
	}

	private LoweredStmt LowerWithCast(StmtNode node) => (LoweredStmt)Lower(node);

	private IEnumerable<LoweredStmt> LowerWithCast(IEnumerable<StmtNode> nodes)
	{
		return nodes.Select(LowerWithCast);
	}

	private LoweredExpr LowerWithCast(ExprNode node) => (LoweredExpr)Lower(node);

	private IEnumerable<LoweredExpr> LowerWithCast(IEnumerable<ExprNode> nodes)
	{
		return nodes.Select(LowerWithCast);
	}

	[Conditional("DEBUG")]
	private void AssertHoistingBagsAreEmpty()
	{
		Debug.Assert(_declsHoistingBag.IsEmpty);
		Debug.Assert(_varStmtsHoistingBag.IsEmpty);
	}

	/// <summary>
	/// Manages local decl hoisting. Should be wrapped around any lowering that can have nested decls that need to be hoisted.
	/// </summary>
	private LoweredDecl HoistLocalDecls<TData, T>(Func<TData, T> func, TData data)
		where T : LoweredDecl
	{
		// Store and restore previous _insideDecl value.
		var wasInside = _insideDecl;
		_insideDecl = true;
		var result = func(data);
		_insideDecl = wasInside;

		if (wasInside)
		{
			// Result is inside another decl, so we hoist it and return an elided decl in its place.
			_declsHoistingBag.Hoist(result);
			return LoweredElidedDecl.Instance;
		}
		else
		{
			// Result is in top level, so we take any hoisted decls that were nested and create a decl list as the result.
			var hoistedDecls = _declsHoistingBag.TakeHoisted();
			if (hoistedDecls.Count == 0)
			{
				// Optimize for the most common case of no hoisted decls. No need to create a LoweredDeclList.
				return result;
			}
			else
			{
				return new LoweredDeclList(result.ParsedNode, [result, .. hoistedDecls]);
			}
		}
	}

	private class HoistingBag<T>
	{
		private List<T> _hoisted = [];

		public HoistingBag()
		{
		}

		public bool IsEmpty => _hoisted.Count == 0;

		public void Hoist(T node)
		{
			_hoisted.Add(node);
		}

		public List<T> TakeHoisted()
		{
			if (IsEmpty)
				return [];

			var hoisted = _hoisted;
			_hoisted = [];
			return hoisted;
		}
	}
}
