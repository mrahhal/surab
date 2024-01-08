using System.Diagnostics;
using Surab.Analysis;

namespace Surab.Compilation;

internal class Lowerer
{
	private readonly Reasoner _reasoner;

	private Lowerer(Reasoner reasoner)
	{
		_reasoner = reasoner;
	}

	internal static LoweredTree Lower(Reasoner reasoner)
	{
		var lowerer = new Lowerer(reasoner);
		return lowerer.Lower();
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
			AliasDeclNode n => new LoweredElidedDecl(n),
			ReturnStmtNode n => Lower(n),
			DeclStmtNode n => Lower(n.Decl),
			ExprStmtNode n => Lower(n),
			GroupingExprNode n => Lower(n.Expr),
			BareExprStmtNode n => Lower(n),
			BlockExprNode n => Lower(n),
			AssignmentExprNode n => Lower(n),
			UnaryExprNode n => Lower(n),
			BinaryExprNode n => Lower(n),
			CallExprNode n => Lower(n),
			AccessExprNode n => Lower(n),
			LiteralExprNode n => Lower(n),
			IdentifierExprNode n => Lower(n),
			ThisExprNode n => Lower(n),
			StructCreationExprNode n => Lower(n),

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

	private LoweredNode Lower(FnDeclNode node)
	{
		var fnSymbol = _reasoner.GetDeclaredSymbol(node) as FnSymbol;
		Debug.Assert(fnSymbol != null);

		return new LoweredFnDecl(
			node,
			fnSymbol,
			node.Body == null ? null : LowerFnBlock(node.Body));
	}

	private LoweredNode Lower(MethodDeclNode node)
	{
		var fnSymbol = _reasoner.GetDeclaredSymbol(node) as FnSymbol;
		Debug.Assert(fnSymbol != null);

		return new LoweredFnDecl(
			node,
			fnSymbol,
			node.Body == null ? null : LowerFnBlock(node.Body));
	}

	private LoweredNode Lower(StructDeclNode node)
	{
		var structTypeSymbol = _reasoner.GetDeclaredSymbol(node) as StructTypeSymbol;
		Debug.Assert(structTypeSymbol != null);

		// TODO: Should support all other member decls.
		return new LoweredTypeDecl(
			node,
			structTypeSymbol,
			structTypeSymbol.Members.OfType<FieldSymbol>(),
			LowerWithCast(node.Members.OfType<MethodDeclNode>()));
	}

	private LoweredNode Lower(VarDeclNode node)
	{
		var symbol = _reasoner.GetDeclaredSymbol(node) as LocalSymbol;
		Debug.Assert(symbol != null);

		var variableStmt = (LoweredStmt)new LoweredVariableDeclareStmt(
			node,
			symbol);

		if (node.Initializer == null)
		{
			return variableStmt;
		}

		var storeStmt = new LoweredVariableStoreStmt(
			node,
			symbol,
			LowerWithCast(node.Initializer.Expr));

		return new LoweredStmtList(
			node,
			[
				variableStmt,
				storeStmt,
			]);
	}

	private LoweredNode Lower(ReturnStmtNode node)
	{
		var expr = node.Expr == null ? null : LowerWithCast(node.Expr);
		return new LoweredReturnStmt(
			node,
			expr);
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

	private LoweredStmtList LowerFnBlock(BlockExprNode node)
	{
		var (stmts, bareReturnExpr) = LowerBlock(node);

		if (bareReturnExpr != null)
		{
			stmts.Add(new LoweredReturnStmt(
				node,
				bareReturnExpr));
		}

		return new LoweredStmtList(
			node,
			stmts);
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
					// The reason this doens't require a bespoke lowered node is because, unlike fields, methods become simple symbols untied to any struct.
					return new LoweredSymbolLoadExpr(
						node,
						fn.Type,
						fn);
				}

			default:
				throw new UnreachableException();
		};
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
		var fnType = (FnTypeSymbol)_reasoner.LookupType(node.Callee).UnwrapAliases();

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
		return new LoweredLiteralExpr(
			node,
			type,
			node.Literal);
	}

	private LoweredNode Lower(IdentifierExprNode node)
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

	private LoweredNode Lower(StructCreationExprNode node)
	{
		// Lowered into LoweredStmtListEndingWithExpr with:
		// - ObjectAllocStmt
		// - FieldStoreStmt[]
		// - CallExpr (init)
		// - Load back object expr

		var stmts = new List<LoweredStmt>();

		var structType = (StructTypeSymbol)_reasoner.LookupType(node).UnwrapAliases();
		var structTempSymbol = new TempSymbol(_reasoner, structType);

		var structLoadExpr = new LoweredSymbolLoadExpr(node, structType, structTempSymbol);

		// Object alloc

		var allocStmt = new LoweredObjectAllocStmt(
			node,
			structType,
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

		foreach (var fieldSymbol in structType.Members.Fields())
		{
			var value = initFieldItems.FirstOrDefault(initF => initF.targetSymbol == fieldSymbol).rhs;
			value ??= LowerWithCast(fieldSymbol.InitializerExprNode!);
			stmts.Add(new LoweredFieldStoreStmt(node, structLoadExpr, fieldSymbol, value));
		}

		// Call init

		if (structType.InitializerMethodDecl != null)
		{
			var initializerFnSymbol = (FnSymbol)_reasoner.GetDeclaredSymbol(structType.InitializerMethodDecl)!;
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

	private LoweredDecl LowerWithCast(DeclNode node) => (LoweredDecl)Lower((SyntaxNode)node);

	private IEnumerable<LoweredDecl> LowerWithCast(IEnumerable<DeclNode> nodes)
	{
		return nodes.Select(LowerWithCast);
	}

	private LoweredStmt LowerWithCast(StmtNode node) => (LoweredStmt)Lower((SyntaxNode)node);

	private IEnumerable<LoweredStmt> LowerWithCast(IEnumerable<StmtNode> nodes)
	{
		return nodes.Select(LowerWithCast);
	}

	private LoweredExpr LowerWithCast(ExprNode node) => (LoweredExpr)Lower((SyntaxNode)node);

	private IEnumerable<LoweredExpr> LowerWithCast(IEnumerable<ExprNode> nodes)
	{
		return nodes.Select(LowerWithCast);
	}
}
