using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Surab.Analysis;

namespace Surab.Compilation;

/// <summary>
/// Lowers from a <see cref="SyntaxTree"/> to a <see cref="HirTree"/>.
///
/// This process simplifies nodes, hoists nested declarations and thus flattening everything,
/// makes implicit generic types (inherited from parents) explicit, desugars some syntax,
/// auto derefs/addresses, auto casts, binds and resolves symbols to simpler values and types.
/// </summary>
internal class HirLowerer
{
	private readonly Reasoner _reasoner;
	private readonly LoweringInterner _interner;

	private readonly HoistingBag<HirDecl> _declsHoistingBag = new();
	private HoistingBag<HirLocalDeclareStmt> _localDeclareStmtsHoistingBag = new();
	private bool _insideDecl;
	private HirValue? _methodReceiverParamValue; // Replacement of `this`.

	private HirLowerer(Reasoner reasoner, LoweringInterner interner)
	{
		_reasoner = reasoner;
		_interner = interner;
	}

	internal static HirLoweredProject LowerProject(SurabProject project, LoweringInterner interner)
	{
		var loweredTrees = project.GetAllReasoners()
			.Select(reasoner => Lower(reasoner, interner))
			.ToList();
		return new HirLoweredProject
		{
			Trees = loweredTrees,
		};
	}

	private static HirTree Lower(Reasoner reasoner, LoweringInterner interner)
	{
		var lowerer = new HirLowerer(reasoner, interner);
		var tree = lowerer.Lower();
		lowerer.AssertHoistingBagsAreEmpty();
		return tree;
	}

	private HirTree Lower()
	{
		var root = Lower(_reasoner.Tree.Root)!;
		return new HirTree(_reasoner.Tree, root);
	}

	private HirNode Lower(SyntaxNode node)
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
			TraitDeclNode => HirElidedDecl.Instance,
			AliasDeclNode => HirElidedDecl.Instance,
			ReturnStmtNode n => Lower(n),
			DeclStmtNode n => Lower(n),
			ExprStmtNode n => Lower(n),
			BareExprStmtNode n => Lower(n),
			EmptyStmtNode => HirElidedStmt.Instance,
			GroupingExprNode n => Lower(n.Expr),
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
			IfExprNode n => Lower(n),

			// All other nodes not in this switch should be taken care of as part of these nodes mentioned above.
			_ => throw new UnreachableException(),
		};

		return result;
	}

	private HirNode Lower(FileNode node)
	{
		return new HirFile(
			node,
			LowerWithCast(node.Decls));
	}

	private HirNode Lower(VarDeclNode node)
	{
		var symbol = _reasoner.GetDeclaredSymbol(node);
		var value = _interner.LowerValue(symbol);
		var localDeclareStmt = new HirLocalDeclareStmt(
			node,
			value);
		_localDeclareStmtsHoistingBag.Hoist(localDeclareStmt);

		if (node.Initializer == null)
		{
			return localDeclareStmt;
		}

		return new HirValueStoreStmt(
			node,
			value,
			LowerWithCast(node.Initializer.Expr));
	}

	private HirNode Lower(FnDeclNode node)
	{
		return HoistLocalDecls((node) =>
		{
			var fnSymbol = _reasoner.GetDeclaredSymbol(node);
			var fnValue = _interner.LowerValue(fnSymbol);
			var body = LowerFnBlock(node.Body);

			return new HirFnDecl(
				node,
				fnValue,
				body);
		}, node);
	}

	private HirNode Lower(MethodDeclNode node)
	{
		return HoistLocalDecls((node) =>
		{
			var fnSymbol = _reasoner.GetDeclaredSymbol(node);
			var fnValue = _interner.LowerValue(fnSymbol);

			var methodReceiverParamValue_prev = _methodReceiverParamValue;
			_methodReceiverParamValue = fnSymbol.MethodReceiverType == null ? null : fnValue.Params[0];

			var receiverType = fnSymbol.MethodReceiverType == null ? null : _interner.LowerType(fnSymbol.MethodReceiverType);
			var body = LowerFnBlock(node.Body);

			_methodReceiverParamValue = methodReceiverParamValue_prev;

			return new HirFnDecl(
				node,
				fnValue,
				body);
		}, node);
	}

	private HirNode Lower(StructDeclNode node)
	{
		return HoistLocalDecls((node) =>
		{
			var namedTypeSymbol = _reasoner.GetDeclaredSymbol(node);
			var type = _interner.LowerType(namedTypeSymbol);
			var nonFieldDeclsDirect = namedTypeSymbol.Members.Where(x => x.Kind is not SymbolKind.Field and not SymbolKind.Impl);
			var nonFieldDeclsInImplBlocks = namedTypeSymbol.Members.OfType<ImplSymbol>().SelectMany(x => x.Members);

			IEnumerable<Symbol> nonFieldDecls = [.. nonFieldDeclsDirect, .. nonFieldDeclsInImplBlocks.Select(x => x.ImplSymbol)];

			foreach (var nonFieldDecl in nonFieldDecls)
			{
				// Non field decls should always be hoisted and so the lowering result should be elided.
				Debug.Assert(nonFieldDecl.DeclaringNode != null);
				var result = Lower(nonFieldDecl.DeclaringNode);
				Debug.Assert(result == HirElidedDecl.Instance);
			}

			return new HirStructDecl(
				node,
				type);
		}, node);
	}

	private HirNode Lower(ReturnStmtNode node)
	{
		var expr = node.Expr == null ? null : LowerWithCast(node.Expr);
		return new HirReturnStmt(
			node,
			expr);
	}

	private HirStmt Lower(DeclStmtNode node)
	{
		return Lower(node.Decl) switch
		{
			// This happens with decls that are lowered to stmts such as a var decl
			// (we'll get either a var store, or an elided stmt since var declare stmt itself will be hoisted).
			HirStmt stmt => stmt,

			// This happens when lowering a nested decl inside a fn for example.
			// This method should always return a stmt, so we simply return an elided stmt instead of the elided decl.
			HirDecl decl when decl == HirElidedDecl.Instance => HirElidedStmt.Instance,

			_ => throw new UnreachableException(),
		};
	}

	private HirNode Lower(ExprStmtNode node)
	{
		// Result might not be an expr (e.g. `a = 2;` it'll be var store stmt), in which case return as is
		// as we won't need to wrap in an LoweredExprStmt in this case.
		var result = Lower(node.Expr);
		if (result is HirExpr expr)
		{
			return new HirExprStmt(
				node,
				expr);
		}

		// Used to get here when `{ ... };` was its own ExprStmtNode, now we separate extraneous
		// semicolons into their own EmptyStmtNode, so we should never get here.
		throw new UnreachableException();
	}

	private HirNode Lower(BareExprStmtNode node)
	{
		var expr = LowerWithCast(node.Expr);
		return new HirBlockReturnStmt(node, expr);
	}

	/// <summary>
	/// Lowers the body of a function or a method.
	/// </summary>
	private HirFnBody? LowerFnBlock(BlockExprNode? node)
	{
		if (node == null)
			return null;

		Debug.Assert(node.Parent is FnDeclNode or MethodDeclNode);

		// Store and restore previous hoisting bag. Imagine a function inside another function.
		var prevBag = _localDeclareStmtsHoistingBag;
		_localDeclareStmtsHoistingBag = new();

		var (stmts, blockReturnExpr) = LowerBlockStmtsAndBlockReturnExpr(node);

		if (blockReturnExpr != null)
		{
			stmts.Add(new HirReturnStmt(
				node,
				blockReturnExpr));
		}

		var hoistedLocalDeclareStmts = _localDeclareStmtsHoistingBag.TakeHoisted();
		_localDeclareStmtsHoistingBag = prevBag;

		return new HirFnBody(
			node,
			[.. hoistedLocalDeclareStmts, .. stmts]);
	}

	private (List<HirStmt> Stmts, HirExpr? BlockReturnExpr) LowerBlockStmtsAndBlockReturnExpr(BlockExprNode node)
	{
		var parsedStmts = node.Stmts.ToList();
		var lastParsedStmt = parsedStmts.LastOrDefault();

		var blockReturnExpr = default(HirExpr?);
		if (CanBeBlockReturnExprStmt(lastParsedStmt, out var bareExprStmt))
		{
			parsedStmts.Remove(lastParsedStmt);
			blockReturnExpr = LowerWithCast(bareExprStmt.Expr);
		}

		var stmts = LowerWithCast(parsedStmts).ToList();

		return (stmts, blockReturnExpr);
	}

	/// <summary>
	/// Returns whether a stmt can be a block return expr stmt.
	/// A <see cref="BareExprStmtNode"/> is considered a block return when it has return type that isn't void.
	/// </summary>
	private bool CanBeBlockReturnExprStmt([NotNullWhen(true)] StmtNode? stmt, [NotNullWhen(true)] out BareExprStmtNode? bareExprStmt)
	{
		if (stmt is BareExprStmtNode bare &&
			_reasoner.LookupType(bare.Expr).KnownTypeTag is not KnownTypeTag.@void)
		{
			bareExprStmt = bare;
			return true;
		}
		bareExprStmt = null;
		return false;
	}

	private HirNode Lower(BlockExprNode node)
	{
		var (stmts, blockReturnExpr) = LowerBlockStmtsAndBlockReturnExpr(node);
		return CreateBlockExpr(node, stmts, blockReturnExpr);
	}

	private HirBlockExpr CreateBlockExpr(SyntaxNode node, List<HirStmt> stmts, HirExpr? blockReturnExpr = null)
	{
		var type = _interner.LowerType(_reasoner.Workspace.ResolveKnownTypeSymbol(KnownTypeTag.@void));
		if (blockReturnExpr != null)
		{
			type = blockReturnExpr.Type;
		}
		return new HirBlockExpr(node, type, stmts, blockReturnExpr);
	}

	private HirNode Lower(AssignmentExprNode node)
	{
		var symbol = _reasoner.LookupSymbol(node.Lhs);
		if (symbol is LocalSymbol or ParamSymbol)
		{
			var value = _interner.LowerValue(symbol);

			var storeStmt = new HirValueStoreStmt(
				node,
				value,
				LowerWithCast(node.Rhs));

			var loadExpr = new HirValueLoadExpr(
				node,
				value.Type,
				value);

			return CreateBlockExpr(
				node,
				[storeStmt],
				loadExpr);
		}
		else if (symbol is FieldSymbol fieldSymbol)
		{
			var value = _interner.LowerValue(fieldSymbol);

			var accessExpr = (AccessExprNode)node.Lhs; // REVIEW: Is this always valid?
			var (loweredTarget, _) = AutoDeref(accessExpr.Target, fieldSymbol.EnclosingType);

			var storeStmt = new HirFieldStoreStmt(
				node,
				loweredTarget,
				value,
				LowerWithCast(node.Rhs));

			var loadExpr = new HirFieldLoadExpr(
				node,
				value.Type,
				loweredTarget,
				value);

			return CreateBlockExpr(
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

	private HirExpr Lower(AccessExprNode node)
	{
		switch (_reasoner.LookupSymbol(node))
		{
			case FieldSymbol field:
				{
					var value = _interner.LowerValue(field);

					var (loweredTarget, _) = AutoDeref(node.Target, field.EnclosingType);
					return new HirFieldLoadExpr(
						node,
						value.Type,
						loweredTarget,
						value);
				}

			case FnSymbol fn:
				{
					var value = _interner.LowerValue(fn);

					// The reason this doesn't require a bespoke lowered node is because, unlike fields, methods become simple symbols untied to any struct.
					return new HirValueLoadExpr(
						node,
						value.Type,
						value);
				}

			default:
				throw new UnreachableException();
		};
	}

	private HirExpr Lower(ModuleAccessExprNode node)
	{
		var symbol = _reasoner.LookupSymbol(node);
		// Should only be here if we're loading a symbol with a type.
		Debug.Assert(symbol is ISymbolHasType);
		var value = _interner.LowerValue(symbol);
		return new HirValueLoadExpr(
			node,
			value.Type,
			value);
	}

	private HirNode Lower(UnaryExprNode node)
	{
		var expr = LowerWithCast(node.Expr);

		if (node.Op.UnaryOperatorTokenKind == UnaryOperatorTokenKind.Deref &&
			expr is HirUnaryExpr loweredExpr && loweredExpr.Op == UnaryOperatorTokenKind.Address)
		{
			// Deref(Address(expr)) cancel each other, so return the Address's inner expr. (`*&a` => `a`)
			return loweredExpr.Expr;
		}

		var typeSymbol = _reasoner.LookupType(node);
		var type = _interner.LowerType(typeSymbol);

		return new HirUnaryExpr(
			node,
			type,
			expr,
			node.Op.UnaryOperatorTokenKind);
	}

	private HirNode Lower(BinaryExprNode node)
	{
		var typeSymbol = _reasoner.LookupType(node);
		var type = _interner.LowerType(typeSymbol);

		return new HirBinaryExpr(
			node,
			type,
			LowerWithCast(node.Left),
			LowerWithCast(node.Right));
	}

	private HirNode Lower(CallExprNode node)
	{
		var calleeExpr = LowerWithCast(node.Callee);

		var calleeSymbol = _reasoner.LookupSymbol(node.Callee);

		// Note: LookupType will give as an FnType that doesn't include the receiver param for non static methods accessed through an instance.
		// (see `FnSymbol.TypeWithoutReceiver`)
		// For this reason, we get back the normal type back in these cases below.
		var fnType = (FnTypeSymbol)_reasoner.LookupType(node.Callee).UnwrapTypeAliases();

		var receiverArgs = Enumerable.Empty<HirExpr>();
		if (calleeSymbol is FnSymbol fnSymbol && node.Callee is AccessExprNode accessExpr && _reasoner.IsInstanceAccess(accessExpr))
		{
			Debug.Assert(fnSymbol.MethodReceiverType != null);
			fnType = fnSymbol.Type; // Set the type to the original type with the receiver param.

			// `a.b()`: We have to do some auto derefing/addressing of `a`.
			var (loweredTarget, loweredTargetType) = AutoDeref(accessExpr.Target, fnSymbol.MethodReceiverType);

			// TODO: Account for @receiver_by_val.
			loweredTarget = AutoAddress(node, loweredTarget, loweredTargetType, fnSymbol.MethodReceiverType);
			receiverArgs = [loweredTarget];
		}

		var type = _interner.LowerType(fnType);

		return new HirCallExpr(
			node,
			type,
			calleeExpr,
			// No auto deref for args. Might be better this way. (If we want it changes will be required in IsAssignableFrom/TypeChecker)
			[.. receiverArgs, .. LowerWithCast(node.Args.Select(arg => arg.Expr))]);
	}

	private HirExpr AutoAddress(SyntaxNode node, HirExpr loweredExpr, TypeSymbol loweredExprType, TypeSymbol targetType)
	{
		// Can only auto address once, unlike auto deref.

		if (targetType.IsAssignableFrom(loweredExprType))
		{
			return loweredExpr;
		}

		var resultType = _reasoner.GetPtrType(loweredExprType);
		var loweredType = _interner.LowerType(resultType);
		return new HirUnaryExpr(node, loweredType, loweredExpr, UnaryOperatorTokenKind.Address);
	}

	private (HirExpr, TypeSymbol) AutoDeref(ExprNode expr, TypeSymbol targetType)
	{
		// TODO: If Type becomes allowed in expr (S.a), this will need an update here.

		var loweredExpr = LowerWithCast(expr);
		var loweredExprType = _reasoner.LookupType(expr);

		var currentType = loweredExprType;
		var derefsNeeded = 0;
		while (currentType is PtrTypeSymbol ptrType && !targetType.IsAssignableFrom(currentType))
		{
			derefsNeeded++;
			currentType = ptrType.InnerType;
		}

		if (derefsNeeded == 0)
		{
			return (loweredExpr, loweredExprType);
		}

		currentType = loweredExprType;
		var currentExpr = loweredExpr;
		for (var i = 0; i < derefsNeeded; i++)
		{
			// currentType can only be a PtrTypeSymbol here.
			// Suppression: Won't be an opaque ptr.
			var newDerefExprType = ((PtrTypeSymbol)currentType).InnerType!;
			var newDerefExprLoweredType = _interner.LowerType(newDerefExprType);
			var newDerefExpr = new HirUnaryExpr(expr, newDerefExprLoweredType, currentExpr, UnaryOperatorTokenKind.Deref);

			currentType = newDerefExprType;
			currentExpr = newDerefExpr;
		}

		return (currentExpr, currentType);
	}

	private HirNode Lower(LiteralExprNode node)
	{
		var typeSymbol = _reasoner.LookupType(node);
		var type = _interner.LowerType(typeSymbol);
		var data = LiteralData.FromLiteralToken(node.Literal);
		return new HirLiteralExpr(
			node,
			type,
			data);
	}

	private HirNode Lower(NameExprNode node)
	{
		// Should only be here if we're loading a value?
		var symbol = _reasoner.LookupSymbol(node);
		Debug.Assert(symbol is ISymbolHasType);
		var value = _interner.LowerValue(symbol);
		return new HirValueLoadExpr(
			node,
			value.Type,
			value);
	}

	private HirNode Lower(ThisExprNode node)
	{
		Debug.Assert(
			!(node.Parent is EqualsExprClauseNode equalsExprClause && equalsExprClause.Expr == node),
			"Should only be here when loading `this` directly as an rvalue.");

		Debug.Assert(_methodReceiverParamValue != null);
		return new HirValueLoadExpr(
			node, _methodReceiverParamValue.Type, _methodReceiverParamValue);
	}

	private HirNode Lower(TypeCreationExprNode node)
	{
		// Lowered into LoweredStmtListEndingWithExpr with:
		// - Hoist LocalDeclareStmt
		// - FieldStoreStmt[]
		// - CallExpr (init)
		// - Load back object expr

		var stmts = new List<HirStmt>();

		var namedTypeSymbol = (NamedTypeSymbol)_reasoner.LookupType(node).UnwrapTypeAliases();
		var namedType = _interner.LowerType(namedTypeSymbol);
		var tempLocalName = "alloc_" + namedType.Identifier;
		// Need to hoist this with the locals otherwise this might not get properly replaced in monomorphization.
		var tempLocalValue = new HirLocalOrParamValue(HirValueKind.Local, tempLocalName, tempLocalName, tempLocalName, namedType);

		// Hoist LocalDeclareStmt

		_localDeclareStmtsHoistingBag.Hoist(new HirLocalDeclareStmt(node, tempLocalValue));

		var structLoadExpr = new HirValueLoadExpr(node, namedType, tempLocalValue);

		var initializerItems = node.ObjectInitializer.Items.Select(item =>
		{
			var targetSymbol = _reasoner.LookupSymbol(item)!;
			return (targetSymbol, expr: LowerWithCast(item.Expr));
		});

		var initFieldItems = initializerItems.Where(x => x.targetSymbol is FieldSymbol);
		var initParamItems = initializerItems.Where(x => x.targetSymbol is ParamSymbol);

		// Field stores

		foreach (var fieldSymbol in namedTypeSymbol.Members.Fields())
		{
			var expr = initFieldItems.FirstOrDefault(initF => initF.targetSymbol == fieldSymbol).expr;
			expr ??= LowerWithCast(fieldSymbol.InitializerExprNode!);
			var fieldValue = _interner.LowerValue(fieldSymbol);
			stmts.Add(new HirFieldStoreStmt(node, structLoadExpr, fieldValue, expr));
		}

		// Call init

		if (namedTypeSymbol.InitializerMethod != null)
		{
			var initializerFnSymbol = namedTypeSymbol.InitializerMethod;
			var initializerFnValue = _interner.LowerValue(initializerFnSymbol);
			IEnumerable<HirExpr> argsSet = initializerFnSymbol.Params.Select(p =>
			{
				var arg = initParamItems.FirstOrDefault(initP => initP.targetSymbol == p).expr;
				arg ??= LowerWithCast(p.InitializerExprNode!);
				return arg;
			});
			// TODO: Account for @receiver_by_val.
			var receiver = AutoAddress(node, structLoadExpr, namedTypeSymbol, initializerFnSymbol.MethodReceiverType!);
			var callInitExpr = new HirCallExpr(
				node,
				initializerFnValue.Type,
				new HirValueLoadExpr(node, initializerFnValue.Type, initializerFnValue),
				[receiver, .. argsSet]);
			stmts.Add(new HirExprStmt(node, callInitExpr));
		}

		// Combine

		return CreateBlockExpr(
			node,
			stmts,
			structLoadExpr);
	}

	private HirNode Lower(IfExprNode node)
	{
		var type = _reasoner.LookupType(node);
		var loweredType = _interner.LowerType(type);
		var arms = new List<HirMatchArm>();

		ExprNode? current = node;
		while (current != null)
		{
			if (current is IfExprNode ifExpr)
			{
				var condition = LowerWithCast(ifExpr.Condition);
				Debug.Assert(ifExpr.ThenBlock != null);
				var block = LowerWithCast(ifExpr.ThenBlock);
				arms.Add(new HirMatchArm(ifExpr, condition, block));
				current = ifExpr.ElseClause?.Expr;
			}
			else if (current is BlockExprNode blockExpr)
			{
				var block = LowerWithCast(blockExpr);
				arms.Add(new HirMatchArm(current, null, block));
				current = null;
			}
			else
			{
				throw new UnreachableException();
			}
		}

		for (var i = 0; i < arms.Count; i++)
		{
			var arm = arms[i];
			var last = i == arms.Count - 1;
			if (!last)
			{
				Debug.Assert(arm.Condition != null);
			}
		}

		return new HirMatchExpr(node, loweredType, arms);
	}

	private HirDecl LowerWithCast(DeclNode node) => (HirDecl)Lower(node);

	private IEnumerable<HirDecl> LowerWithCast(IEnumerable<DeclNode> nodes)
	{
		return UnwrapContainersAndSkipElided(nodes.Select(LowerWithCast));

		static IEnumerable<HirDecl> UnwrapContainersAndSkipElided(IEnumerable<HirDecl> decls)
		{
			foreach (var decl in decls)
			{
				if (decl is HirElidedDecl)
					continue;
				else if (decl is HirDeclList declList)
					foreach (var decl2 in UnwrapContainersAndSkipElided(declList.Decls))
						yield return decl2;
				else
					yield return decl;
			}
		}
	}

	private HirStmt LowerWithCast(StmtNode node) => (HirStmt)Lower(node);

	private IEnumerable<HirStmt> LowerWithCast(IEnumerable<StmtNode> nodes)
	{
		return UnwrapContainersAndSkipElided(nodes.Select(LowerWithCast));

		static IEnumerable<HirStmt> UnwrapContainersAndSkipElided(IEnumerable<HirStmt> stmts)
		{
			foreach (var stmt in stmts)
			{
				if (stmt is HirElidedStmt)
					continue;
				else if (stmt is HirStmtList stmtList)
					foreach (var stmt2 in UnwrapContainersAndSkipElided(stmtList.Stmts))
						yield return stmt2;
				else
					yield return stmt;
			}
		}
	}

	private HirExpr LowerWithCast(ExprNode node) => (HirExpr)Lower(node);

	private IEnumerable<HirExpr> LowerWithCast(IEnumerable<ExprNode> nodes)
	{
		return nodes.Select(LowerWithCast);
	}

	[Conditional("DEBUG")]
	private void AssertHoistingBagsAreEmpty()
	{
		Debug.Assert(_declsHoistingBag.IsEmpty);
		Debug.Assert(_localDeclareStmtsHoistingBag.IsEmpty);
	}

	/// <summary>
	/// Manages local decl hoisting. Should be wrapped around any lowering that can have nested decls that need to be hoisted.
	/// </summary>
	private HirDecl HoistLocalDecls<TData, T>(Func<TData, T> func, TData data)
		where T : HirDecl
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
			return HirElidedDecl.Instance;
		}
		else
		{
			// Result is in top level, so we take any hoisted decls that were nested and create a decl list as the result.
			var hoistedDecls = _declsHoistingBag.TakeHoisted();
			if (hoistedDecls.Count == 0)
			{
				// Optimize for the most common case of no hoisted decls. No need to create a list.
				return result;
			}
			else
			{
				return new HirDeclList(result.SyntaxNode, [result, .. hoistedDecls]);
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
