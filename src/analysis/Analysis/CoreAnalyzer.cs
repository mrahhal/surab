using System.Diagnostics;

namespace Surab.Analysis;

/// <summary>
/// A visitor that detects all core semantic diagnostics in a <see cref="Reasoner"/>.
/// </summary>
internal class CoreAnalyzer(Reasoner reasoner) : SyntaxTreeVisitor
{
	private readonly Reasoner _reasoner = reasoner;
	private readonly DiagnosticBag _diagnostics = new();

	public static DiagnosticBag Analyze(Reasoner reasoner)
	{
		var analyzer = new CoreAnalyzer(reasoner);
		analyzer.Analyze();
		return analyzer._diagnostics;
	}

	private void Analyze()
	{
		Visit(_reasoner.Tree.Root);
	}

	public override void VisitConstDecl(ConstDeclNode node)
	{
		// Temp: Should be allowed only in top level.
		if (node.Parent is not ModuleNode)
		{
			_diagnostics.Add(node.Span, DiagnosticLevel.Error, $"a const decl is allowed only in top level");
		}

		base.VisitConstDecl(node);
	}

	public override void VisitFnDecl(FnDeclNode node)
	{
		// Temp: Should be allowed only in top level.
		if (node.Parent is not ModuleNode)
		{
			_diagnostics.Add(node.Span, DiagnosticLevel.Error, $"a function decl is allowed only in top level");
		}

		base.VisitFnDecl(node);
	}

	public override void VisitClassDecl(ClassDeclNode node)
	{
		// Temp: Should be allowed only in top level.
		if (node.Parent is not ModuleNode)
		{
			_diagnostics.Add(node.Span, DiagnosticLevel.Error, $"a class decl is allowed only in top level");
		}

		base.VisitClassDecl(node);
	}

	public override void VisitFieldDecl(FieldDeclNode node)
	{
		if (node.Parent is not DeclMembersNode declMembersNode || declMembersNode.Parent is not StructDeclNode or ClassDeclNode)
		{
			_diagnostics.Add(node.Span, DiagnosticLevel.Error, $"a field decl is not allowed in this context");
		}

		base.VisitFieldDecl(node);
	}

	public override void VisitMethodDecl(MethodDeclNode node)
	{
		if (node.Parent is not ClassDeclNode)
		{
			_diagnostics.Add(node.Span, DiagnosticLevel.Error, $"a method decl is not allowed in this context");
		}

		base.VisitMethodDecl(node);
	}

	public override void VisitReturnStmt(ReturnStmtNode node)
	{
		Debug.Assert(node.Parent is BlockExprNode);
		var block = (BlockExprNode)node.Parent;

		// If it's not the last stmt, warn.
		// TODO: Removed fror now, as this needs control flow analysis.
		// Example: `if () { return 1; } else { return 2; }`?
		//if (block.Stmts.Last() != node)
		//{
		//    _diagnostics.Add(node.Span, DiagnosticLevel.Warning, $"unreachable code after the return stmt");
		//}

		// TODO: Error if return has incompatible type with function return type.

		base.VisitReturnStmt(node);
	}

	public override void VisitBlockExpr(BlockExprNode node)
	{
		// If this block returns an expr, it can't stand on its own.
		// i.e. It shouldn't be parented by ExprStmtNode or BareExprStmtNode.
		if (node.ReturnsExpr &&
			node.Parent is ExprStmtNode or BareExprStmtNode)
		{
			_diagnostics.Add(
				node.Span, DiagnosticLevel.Error,
				$"a block returning an expr can not stand on its own");
		}

		base.VisitBlockExpr(node);
	}

	public override void VisitBareExprStmt(BareExprStmtNode node)
	{
		// Should only be allowed as the last stmt unless this bare is treated as a stmt (i.e. a block without
		// a bare expr (because the block gets lowered to a list of stmts in that case).

		Debug.Assert(node.Parent is BlockExprNode);
		var parentBlock = (BlockExprNode)node.Parent;

		if (!node.IsTreatedAsStmt && parentBlock.Stmts.Last() != node)
		{
			_diagnostics.Add(node.Span, DiagnosticLevel.Error, $"a bare expr can only exist as the last stmt of a block");
		}

		base.VisitBareExprStmt(node);
	}

	public override void VisitVarStmt(VarStmtNode node)
	{
		var lhsSymbol = (LocalSymbol)_reasoner.GetDeclaredSymbol(node)!;
		var lhsType = lhsSymbol.Type;

		if (node.Initializer != null)
		{
			var rhsType = _reasoner.LookupType(node.Initializer.Expr);

			AnalyzeAssignment(node, lhsType, rhsType);
		}

		base.VisitVarStmt(node);
	}

	public override void VisitAssignmentExpr(AssignmentExprNode node)
	{
		// Compatibility
		//var lhsSymbol = _reasoner.LookupSymbol(node.Lhs);
		// TODO: Use lhsSymbol.Type if lhsSymbol has a Type?
		var lhsType = _reasoner.LookupType(node.Lhs);

		//var rhsSymbol = _reasoner.LookupSymbol(node.Rhs);
		var rhsType = _reasoner.LookupType(node.Rhs);

		AnalyzeAssignment(node, lhsType, rhsType);

		// TODO (SemanticAnalyzer): Invalid target of assignment.
		/*switch (node.Lhs)
		{
			//case AccessExprNode accessExprNode:
			//    break;
			case IdentifierExprNode identifierExprNode:
				break;

			default:
				Debug.Assert(false, "Invalid target of assignment.");
				throw new NotImplementedException();
		}*/

		base.VisitAssignmentExpr(node);
	}

	private void AnalyzeAssignment(SyntaxNode node, TypeSymbol lhsType, TypeSymbol rhsType)
	{
		if (lhsType.IsError || rhsType.IsError)
		{
			// Reported somewhere else.
		}
		else if (!lhsType.IsAssignableFrom(rhsType))
		{
			// Incompatible types
			_diagnostics.Add(node.Span, DiagnosticLevel.Error, $"invalid assignment because of incompatible types, expected `{lhsType?.Name}` but received `{rhsType?.Name}`");
		}
	}

	public override void VisitUnaryExpr(UnaryExprNode node)
	{
		// TODO: Should work on traits or something.

		var exprType = _reasoner.LookupType(node.Expr);

		switch (node.Op.UnaryOperatorTokenKind)
		{
			case UnaryOperatorTokenKind.Pos:
			case UnaryOperatorTokenKind.Neg:
				if (!BuiltinTypeSystem.IsNumeric(exprType))
				{
					_diagnostics.Add(node.Op.Span, DiagnosticLevel.Error, $"only valid with a numeric type");
				}
				break;
			case UnaryOperatorTokenKind.Not:
				if (exprType != BuiltinTypeSystem.@bool)
				{
					_diagnostics.Add(node.Op.Span, DiagnosticLevel.Error, $"only valid with a bool type");
				}
				break;
		}

		base.VisitUnaryExpr(node);
	}

	public override void VisitBinaryExpr(BinaryExprNode node)
	{
		var leftType = _reasoner.LookupType(node.Left);
		var rightType = _reasoner.LookupType(node.Right);

		if (leftType.IsError || rightType.IsError)
		{
			// Shouldn't report. Will be reported elsewhere.
		}
		else if (!leftType.IsAssignableFrom(rightType))
		{
			_diagnostics.Add(node.Span, DiagnosticLevel.Error, $"incompatible types");
		}
		else
		{
			var exprType = leftType;
			switch (node.Op.BinaryOperatorTokenKind)
			{
				case BinaryOperatorTokenKind.Add:
				case BinaryOperatorTokenKind.Sub:
				case BinaryOperatorTokenKind.Mul:
				case BinaryOperatorTokenKind.Div:
				case BinaryOperatorTokenKind.Pow:
				case BinaryOperatorTokenKind.BitAnd:
				case BinaryOperatorTokenKind.BitOr:
				case BinaryOperatorTokenKind.BitXor:
					if (!BuiltinTypeSystem.IsNumeric(exprType))
					{
						_diagnostics.Add(node.Op.Span, DiagnosticLevel.Error, $"only valid with a numeric type");
					}
					break;
				case BinaryOperatorTokenKind.And:
				case BinaryOperatorTokenKind.Or:
					if (exprType != BuiltinTypeSystem.@bool)
					{
						_diagnostics.Add(node.Op.Span, DiagnosticLevel.Error, $"only valid with a bool type");
					}
					break;
				//case BinaryOperatorTokenKind.Eq:
				//    break;
				//case BinaryOperatorTokenKind.Ne:
				//    break;
				case BinaryOperatorTokenKind.Lt:
				case BinaryOperatorTokenKind.Le:
				case BinaryOperatorTokenKind.Gt:
				case BinaryOperatorTokenKind.Ge:
					if (!BuiltinTypeSystem.IsNumeric(exprType))
					{
						_diagnostics.Add(node.Op.Span, DiagnosticLevel.Error, $"only valid with a numeric type");
					}
					break;

				default:
					break;
			}
		}

		base.VisitBinaryExpr(node);
	}

	public override void VisitCallExpr(CallExprNode node)
	{
		var calleeType = _reasoner.LookupType(node.Callee).UnwrapAliases();

		// Ensure callee is a function and validate args.

		if (calleeType.IsError)
		{
			// Reported somewhere else.
		}
		else if (calleeType is not FnTypeSymbol)
		{
			_diagnostics.Add(node.Callee.Span, DiagnosticLevel.Error, $"invocation on a non function");
		}
		else if (calleeType is FnTypeSymbol fnSymbol)
		{
			var args = node.Args.Args.ToList();
			if (fnSymbol.ParamTypes.Length != args.Count)
			{
				_diagnostics.Add(node.Args.Span, DiagnosticLevel.Error, $"invalid invocation: wrong number of arguments");
			}
			else
			{
				for (var i = 0; i < args.Count; i++)
				{
					var arg = args[i];
					var paramType = fnSymbol.ParamTypes[i];
					var argType = _reasoner.LookupType(arg);
					if (paramType.IsError || argType.IsError)
					{
						// Reported somewhere else.
					}
					else if (!paramType.IsAssignableFrom(argType))
					{
						_diagnostics.Add(arg.Span, DiagnosticLevel.Error, $"incompatible type for arg, expected `{paramType.Name}` but received `{argType?.Name}`");
					}
				}
			}
		}

		base.VisitCallExpr(node);
	}

	public override void VisitAccessExpr(AccessExprNode node)
	{
		var targetType = _reasoner.LookupType(node.Target);
		if (targetType.IsError)
		{
			// Reported somewhere else.
			return;
		}

		//if (targetType ??)
		//{
		//    _diagnostics.Add(node.Span, DiagnosticLevel.Error, $"unallowed access");
		//    return; // Should we return?
		//}

		// TODO: Store and use a lookup table for this?
		var matched = targetType.GetMember(node.Identifier.Value);
		if (matched == null)
		{
			_diagnostics.Add(node.Identifier.Span, DiagnosticLevel.Error, $"unknown member access");
		}

		base.VisitAccessExpr(node);
	}

	public override void VisitThisExpr(ThisExprNode node)
	{
		var thisType = _reasoner.LookupType(node);
		if (thisType == null)
		{
			_diagnostics.Add(node.Span, DiagnosticLevel.Error, $"unknown this context");
		}

		base.VisitThisExpr(node);
	}

	public override void VisitStructCreationExpr(StructCreationExprNode node)
	{
		var symbol = _reasoner.LookupSymbol(node);
		AnalyzeLookedUpIdentifier(node.Identifier, symbol);

		if (symbol == null)
		{
			// If symbol is null, skip analyzing lhs for each initializer.
			foreach (var initializerExpr in node.MapInitializer.WellFormedExprs)
			{
				Visit(initializerExpr.Rhs);
			}
			return;
		}

		base.VisitStructCreationExpr(node);
	}

	public override void VisitParam(ParamNode node)
	{
		if (node.Type == null)
		{
			_diagnostics.Add(node.Span, DiagnosticLevel.Error, $"specifying type is required");
		}

		base.VisitParam(node);
	}

	public override void VisitFnSig(FnSigNode node)
	{
		base.VisitFnSig(node);
	}

	public override void VisitIdentifierExpr(IdentifierExprNode node)
	{
		var symbol = _reasoner.LookupSymbol(node);
		AnalyzeLookedUpIdentifier(node.Identifier, symbol);

		base.VisitIdentifierExpr(node);
	}

	private void AnalyzeLookedUpIdentifier(IdentifierToken identifier, Symbol? symbol)
	{
		if (symbol == null)
		{
			_diagnostics.Add(identifier.Span, DiagnosticLevel.Error, $"unknown name");
		}
	}

	public override void VisitNamedType(NamedTypeNode node)
	{
		var symbol = _reasoner.LookupSymbol(node);
		if (symbol == null)
		{
			_diagnostics.Add(node.Span, DiagnosticLevel.Error, $"unknown type");
		}
		else if (symbol is not TypeSymbol)
		{
			_diagnostics.Add(node.Span, DiagnosticLevel.Error, $"not a type");
		}

		base.VisitNamedType(node);
	}

	public override void VisitFnType(FnTypeNode node)
	{
		base.VisitFnType(node);
	}

	// TODO: Things to consider adding here.
	// - LetStmtNode.Type and Initializer:
	//   - Initializer should always be given a value as a start
	// - TypeSymbol being the ErrorType
	// - BlockExpr can't appear as an l-value
	// - Binary/Unary
	// - Number assignment problems (u32 = u64 literal)
	// - Function: not all code paths return a value `fn f() i32 { }`
}
