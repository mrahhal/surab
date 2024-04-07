using System.Diagnostics;

namespace Surab.Analysis;

/// <summary>
/// The main type checker detects all semantic diagnostics in a <see cref="Reasoner"/>.
/// </summary>
internal class TypeChecker(Reasoner reasoner) : SyntaxTreeVisitor
{
	private readonly Reasoner _reasoner = reasoner;
	private readonly DiagBag _diags = new();

	public static DiagBag Analyze(Reasoner reasoner)
	{
		var analyzer = new TypeChecker(reasoner);
		analyzer.Analyze();
		return analyzer._diags;
	}

	private void Analyze()
	{
		Visit(_reasoner.Tree.Root);
	}

	public override void VisitImport(ImportNode node)
	{
		if (node.Target is not IdentifierExprNode and not AccessExprNode)
		{
			_diags.Add(node.Target.Span, DiagLevel.Error, $"invalid syntax for import");
		}
		else if (_reasoner.ResolveImport(node).IsFailed)
		{
			_diags.Add(node.Target.Span, DiagLevel.Error, $"failed to resolve module to import");
		}

		// TODO: Need a way to try resolve an import that connects back to the SurabProject somehow.

		//base.VisitImport(node);
	}

	public override void VisitConstDecl(ConstDeclNode node)
	{
		base.VisitConstDecl(node);
	}

	public override void VisitVarDecl(VarDeclNode node)
	{
		var logicalEnclosure = node.GetLogicalEnclosure();
		if (logicalEnclosure == null || !logicalEnclosure.Kind.CanContainExecutableCode())
		{
			_diags.Add(TextSpan.FromTo(node.VarToken.Span, node.Identifier.Span), DiagLevel.Error, $"invalid context for a var declaration");
			return;
		}

		var lhsSymbol = (LocalSymbol)_reasoner.GetDeclaredSymbol(node)!;
		var lhsType = lhsSymbol.Type;

		if (node.Initializer != null)
		{
			var rhsSymbol = _reasoner.LookupSymbol(node.Initializer.Expr);
			var rhsType = _reasoner.LookupType(node.Initializer.Expr);

			AnalyzeAssignment(node, lhsSymbol, lhsType, rhsSymbol, rhsType);
		}

		base.VisitVarDecl(node);
	}

	public override void VisitFnDecl(FnDeclNode node)
	{
		base.VisitFnDecl(node);
	}

	public override void VisitStructDecl(StructDeclNode node)
	{
		base.VisitStructDecl(node);
	}

	public override void VisitClassDecl(ClassDeclNode node)
	{
		base.VisitClassDecl(node);
	}

	public override void VisitFieldDecl(FieldDeclNode node)
	{
		if (!IsMemberOfStructOrClass(node))
		{
			_diags.Add(node.Span, DiagLevel.Error, $"a field decl is not allowed in this context");
		}

		base.VisitFieldDecl(node);
	}

	public override void VisitMethodDecl(MethodDeclNode node)
	{
		if (!IsMemberOfStructOrClass(node))
		{
			_diags.Add(node.Span, DiagLevel.Error, $"a method decl is not allowed in this context");
		}

		var fnSymbol = _reasoner.GetDeclaredSymbol(node) as FnSymbol;
		var enclosingType = fnSymbol?.Parent as TypeSymbol;
		Debug.Assert(enclosingType != null);

		if (node.IsInitializer)
		{
			if (enclosingType.InitializerMethodDecl == node)
			{
				// Report conflicting initializer param if it conflicts with a field.
				var conflictingParams = enclosingType.CreationMemberSymbols.Where(x => x.IsConflicting);
				foreach (var conflicting in conflictingParams)
				{
					// TODO: Better diag message.
					_diags.Add(conflicting.Symbol.DeclaringNode!.Span, DiagLevel.Error, $"init member conflicts with another init member");
				}
			}
			else
			{
				// Report more than one initializer.
				_diags.Add(node.Sig.ParamsList.Span, DiagLevel.Error, $"only one initializer is allowed on a type");
			}
		}

		base.VisitMethodDecl(node);
	}

	private bool IsMemberOfStructOrClass(SyntaxNode node)
	{
		return node.Parent is DeclsListNode declsList && declsList.Parent is StructDeclNode or ClassDeclNode;
	}

	public override void VisitReturnStmt(ReturnStmtNode node)
	{
		// TODO: Lambdas
		var decl = node.Ancestors().Where(x => x is MethodDeclNode or FnDeclNode).FirstOrDefault();
		if (decl == null)
		{
			_diags.Add(node.Span, DiagLevel.Error, $"invalid context for a return statement");
			return;
		}
		var returnTypeNode = decl switch
		{
			MethodDeclNode methodDecl => methodDecl.Sig.ReturnType,
			FnDeclNode fnDecl => fnDecl.Sig.ReturnType,
			_ => throw new UnreachableException(),
		};

		var noneType = _reasoner.Project.ResolveKnownTypeSymbol(KnownTypeTag.None);
		var fnReturnType = returnTypeNode == null ? noneType : _reasoner.LookupSymbol(returnTypeNode).AsTypeSymbolOrUnknown(_reasoner);
		var returnType = node.Expr == null ? noneType : _reasoner.LookupType(node.Expr);

		if (!fnReturnType.IsAssignableFrom(returnType))
		{
			_diags.Add(node.Span, DiagLevel.Error, $"return type incompatible with function return type, expected `{fnReturnType}` but returning `{returnType}`");
		}

		base.VisitReturnStmt(node);
	}

	public override void VisitBlockExpr(BlockExprNode node)
	{
		// If this block returns an expr, it can't stand on its own.
		// i.e. It shouldn't be parented by ExprStmtNode or BareExprStmtNode.
		if (node.ReturnsExpr &&
			node.Parent is ExprStmtNode or BareExprStmtNode)
		{
			_diags.Add(
				node.Span, DiagLevel.Error,
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
			_diags.Add(node.Span, DiagLevel.Error, $"a bare expr can only exist as the last stmt of a block");
		}

		base.VisitBareExprStmt(node);
	}

	public override void VisitAssignmentExpr(AssignmentExprNode node)
	{
		var lhsSymbol = _reasoner.LookupSymbol(node.Lhs);
		var lhsType = _reasoner.LookupType(node.Lhs);

		var rhsSymbol = _reasoner.LookupSymbol(node.Rhs);
		var rhsType = _reasoner.LookupType(node.Rhs);

		AnalyzeAssignment(node, lhsSymbol, lhsType, rhsSymbol, rhsType);

		// TODO (SemanticAnalyzer): Invalid target of assignment (not a valid lvalue).
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

	private void AnalyzeAssignment(SyntaxNode node, Symbol? lhsSymbol, TypeSymbol lhsType, Symbol? rhsSymbol, TypeSymbol rhsType)
	{
		// TODO: Add a test later including:
		// ```
		// var a: i32 = mod1; // error
		// var b = mod1; // error
		// mod1 = 2; // error
		// var mod1 = 3;
		// mod1 = 2;
		// ```

		if (lhsSymbol is ModuleSymbol)
		{
			_diags.Add(node.Span, DiagLevel.Error, $"A module cannot be used as an l-value.");
		}
		else if (rhsSymbol is ModuleSymbol)
		{
			_diags.Add(node.Span, DiagLevel.Error, $"A module cannot be used as an r-value.");
		}
		else if (lhsType.IsUnknown || rhsType.IsUnknown)
		{
			// Reported somewhere else?
		}
		else if (!lhsType.IsAssignableFrom(rhsType))
		{
			// Incompatible types
			_diags.Add(node.Span, DiagLevel.Error, $"invalid assignment because of incompatible types, expected `{lhsType}` but received `{rhsType}`");
		}
		else if (rhsSymbol is TypeSymbol)
		{
			_diags.Add(node.Span, DiagLevel.Error, $"Invalid assignment of a type. Did you mean to create the type instead using `Type {{ ... }}`?");
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
				if (!exprType.IsNumeric())
				{
					_diags.Add(node.Op.Span, DiagLevel.Error, $"only valid with a numeric type");
				}
				break;
			case UnaryOperatorTokenKind.Not:
				if (exprType.KnownTypeTag != KnownTypeTag.@bool)
				{
					_diags.Add(node.Op.Span, DiagLevel.Error, $"only valid with a bool type");
				}
				break;
		}

		base.VisitUnaryExpr(node);
	}

	public override void VisitBinaryExpr(BinaryExprNode node)
	{
		var leftType = _reasoner.LookupType(node.Left);
		var rightType = _reasoner.LookupType(node.Right);

		if (leftType.IsUnknown || rightType.IsUnknown)
		{
			// Shouldn't report. Will be reported elsewhere.
		}
		else if (!leftType.IsAssignableFrom(rightType))
		{
			_diags.Add(node.Span, DiagLevel.Error, $"incompatible types");
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
					if (!exprType.IsNumeric())
					{
						_diags.Add(node.Op.Span, DiagLevel.Error, $"only valid with a numeric type");
					}
					break;
				case BinaryOperatorTokenKind.And:
				case BinaryOperatorTokenKind.Or:
					if (exprType.KnownTypeTag != KnownTypeTag.@bool)
					{
						_diags.Add(node.Op.Span, DiagLevel.Error, $"only valid with a bool type");
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
					if (!exprType.IsNumeric())
					{
						_diags.Add(node.Op.Span, DiagLevel.Error, $"only valid with a numeric type");
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
		var calleeType = _reasoner.LookupType(node.Callee).UnwrapTypeAliases();

		// Ensure callee is a function and validate args.

		if (calleeType.IsUnknown)
		{
			// Reported somewhere else.
		}
		else if (calleeType is not FnTypeSymbol)
		{
			_diags.Add(node.Callee.Span, DiagLevel.Error, $"invocation on a non function");
		}
		else if (calleeType is FnTypeSymbol fnType)
		{
			var args = node.Args.ToList();
			var paramTypes = fnType.ParamTypes;

			if (paramTypes.Length != args.Count)
			{
				_diags.Add(node.ArgsList.Span, DiagLevel.Error, $"invalid invocation: wrong number of arguments");
			}
			else
			{
				for (var i = 0; i < args.Count; i++)
				{
					var arg = args[i];
					var paramType = paramTypes[i];
					var argType = _reasoner.LookupType(arg);
					if (paramType.IsUnknown || argType.IsUnknown)
					{
						// Reported somewhere else.
					}
					else if (!paramType.IsAssignableFrom(argType))
					{
						_diags.Add(arg.Span, DiagLevel.Error, $"incompatible type for arg, expected `{paramType}` but received `{argType}`");
					}
				}
			}
		}

		base.VisitCallExpr(node);
	}

	public override void VisitAccessExpr(AccessExprNode node)
	{
		var targetSymbol = _reasoner.LookupSymbol(node.Target);
		if (targetSymbol is ModuleSymbol)
		{
			_diags.Add(node.Identifier.Span, DiagLevel.Error, $"invalid access on a module, did you mean to use :: instead?");
			return;
		}

		var targetType = _reasoner.LookupType(node.Target);
		if (targetType.IsUnknown)
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
		var member = targetType.GetMember(node.Identifier.Value);
		if (member == null)
		{
			_diags.Add(node.Identifier.Span, DiagLevel.Error, $"unknown member access");
		}
		if (member is FnSymbol && _reasoner.IsInstanceAccess(node))
		{
			if (node.Parent is not CallExprNode)
			{
				_diags.Add(node.Identifier.Span, DiagLevel.Error, $"taking a value of a method is not allowed, call it with `()`");
			}
		}

		base.VisitAccessExpr(node);
	}

	public override void VisitModuleAccessExpr(ModuleAccessExprNode node)
	{
		var targetSymbol = _reasoner.LookupModule(node.Target);
		if (targetSymbol is not ModuleSymbol module)
		{
			_diags.Add(TextSpan.FromTo(node.Target.Span, node.ColonColonToken.Span), DiagLevel.Error, $"not a module");
			return;
		}

		var result = module.GetExportedSymbol(node.Identifier);
		if (result == null)
		{
			_diags.Add(node.Identifier.Span, DiagLevel.Error, $"can't find exported symbol");
			return;
		}

		// TODO: "Did you mean to use . to access module hierarchy?" (b/c similar to rust).

		base.VisitModuleAccessExpr(node);
	}

	public override void VisitThisExpr(ThisExprNode node)
	{
		var thisType = _reasoner.LookupType(node);
		if (thisType == null)
		{
			_diags.Add(node.Span, DiagLevel.Error, $"unknown this context");
		}

		base.VisitThisExpr(node);
	}

	public override void VisitTypeCreationExpr(TypeCreationExprNode node)
	{
		var symbol = _reasoner.LookupSymbol(node.TypeExpr);
		if (symbol == null)
		{
			_diags.Add(node.TypeExpr.Span, DiagLevel.Error, $"unknown type to create");
			return;
		}
		if (symbol is not TypeSymbol typeSymbol)
		{
			// If type symbol is null, skip analyzing lhs for each initializer item as we won't be able to resolve them anyway.
			// Do analyze rhs.
			foreach (var initializerExpr in node.MapInitializer.Items.Select(x => x.Expr as AssignmentExprNode))
			{
				if (initializerExpr != null)
				{
					Visit(initializerExpr.Rhs);
				}
			}

			_diags.Add(node.TypeExpr.Span, DiagLevel.Error, $"not a type that can be created");
			return;
		}

		// Report unset required symbols.
		var setSymbols = node.MapInitializer
			.Items
			.Select(x => x.Expr as AssignmentExprNode).Where(x => x != null)
			.Select(x => (Assignment: x!, Symbol: _reasoner.LookupSymbol(x!.Lhs)!))
			.Where(x => x.Symbol != null)
			.ToList();
		var setSymbolsMap = setSymbols.ToDictionary(x => x.Symbol!, x => x.Assignment);
		var creationSymbols = typeSymbol.CreationMemberSymbols.Select(x => x.Symbol).ToList();
		var requiredSymbols = typeSymbol.CreationMemberSymbols.Where(x => !x.IsOptional && !x.IsConflicting).Select(x => x.Symbol);
		var unsetSymbols = requiredSymbols.Where(requiredSymbol => !setSymbolsMap.ContainsKey(requiredSymbol)).ToList();
		if (unsetSymbols.Count != 0)
		{
			var missingText = string.Join(", ", unsetSymbols.Select(x => x.ToString()));
			_diags.Add(node.TypeExpr.Span, DiagLevel.Error, $"missing required members in initializer: {missingText}");
		}

		// Report set symbols that are not creation symbols.
		var invalidSetSymbols = setSymbols.Where(x => !creationSymbols.Contains(x.Symbol)).ToList();
		if (invalidSetSymbols.Count != 0)
		{
			foreach (var invalidSetSymbol in invalidSetSymbols)
			{
				var assignmentNode = setSymbolsMap[invalidSetSymbol.Symbol];
				_diags.Add(assignmentNode.Span, DiagLevel.Error, $"invalid init target");
			}
		}

		base.VisitTypeCreationExpr(node);
	}

	public override void VisitMapInitializerItem(MapInitializerItemNode node)
	{
		if (node.Expr is not AssignmentExprNode)
		{
			_diags.Add(node.Span, DiagLevel.Error, $"invalid assignment in initializer");
		}

		base.VisitMapInitializerItem(node);
	}

	public override void VisitParam(ParamNode node)
	{
		if (node.Type == null)
		{
			_diags.Add(node.Span, DiagLevel.Error, $"specifying type is required");
		}

		base.VisitParam(node);
	}

	public override void VisitFnSig(FnSigNode node)
	{
		// TODO: Analyze code paths in body and add diag for if not all code paths return
		// (without looking into the return type as that is an error on the return itself).

		// TODO: Unreachable code warning.

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
			_diags.Add(identifier.Span, DiagLevel.Error, $"unknown name");
		}
	}

	public override void VisitExprType(ExprTypeNode node)
	{
		var symbol = _reasoner.LookupSymbol(node);
		if (symbol == null)
		{
			_diags.Add(node.Span, DiagLevel.Error, $"unknown type");
		}
		else if (symbol is not TypeSymbol)
		{
			_diags.Add(node.Span, DiagLevel.Error, $"not a type");
		}

		base.VisitExprType(node);
	}

	public override void VisitFnType(FnTypeNode node)
	{
		base.VisitFnType(node);
	}

	// TODO: Things to consider adding here.
	// - LetStmtNode.Type and Initializer:
	//   - Initializer should always be given a value as a start
	// - TypeSymbol being the Unknown
	// - BlockExpr can't appear as an l-value
	// - Binary/Unary
	// - Number assignment problems (u32 = u64 literal)
	// - Function: not all code paths return a value `fn f() i32 { }`
}
