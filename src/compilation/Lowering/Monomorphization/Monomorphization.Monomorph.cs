using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Surab.Compilation;

// The intention is that when codegening there will only be mono values and types, whether in
// declaration or in usage. This means generic decls need to be monomorphed into multiple mono ones,
// and closed generic usages need to be replaced with the mono value or type.

internal class MonomorphizationMonomorph : HirTreeVisitor
{
	private readonly HirLoweredProject _project;
	private readonly MonomorphizationCollection _collection;
	private readonly LoweringInterner _interner;

	private readonly List<HirTree> _trees = [];

	private MonomorphizationMonomorph(HirLoweredProject project, MonomorphizationCollection collection, LoweringInterner interner)
	{
		_project = project;
		_collection = collection;
		_interner = interner;
	}

	public static IReadOnlyList<HirTree> Monomorph(HirLoweredProject project, MonomorphizationCollection collection, LoweringInterner interner)
	{
		var m = new MonomorphizationMonomorph(project, collection, interner);
		m.Monomorph();
		return m._trees;
	}

	private void Monomorph()
	{
		foreach (var tree in _project.Trees)
		{
			var root = RewriteFile((HirFile)tree.Root);
			_trees.Add(new HirTree(tree.SyntaxTree, root));
		}
	}

	private HirFile RewriteFile(HirFile node)
	{
		var decls = new List<HirDecl>();
		foreach (var decl in node.Decls)
		{
			decls.AddRange(RewriteDecl(decl));
		}
		return new HirFile(node.SyntaxNode, decls);
	}

	private List<HirDecl> RewriteDecl(HirDecl decl)
	{
		if (decl is HirDeclList declList)
		{
			return declList.Decls.SelectMany(RewriteDecl).ToList();
		}
		else if (decl is HirElidedDecl)
		{
			return [];
		}
		else if (decl is HirStructDecl structDecl)
		{
			var rewrittenDecls = RewriteStructDecl(structDecl);
			Debug.Assert(rewrittenDecls.All(d => d.Type is not HirGenericStructType));
			return [.. rewrittenDecls];
		}
		else if (decl is HirFnDecl fnDecl)
		{
			var rewrittenDecls = RewriteFnDecl(fnDecl);
			Debug.Assert(rewrittenDecls.All(d => d.FnValue is not HirGenericFnValue));
			return [.. rewrittenDecls];
		}

		throw new UnreachableException();
	}

	private List<HirStructDecl> RewriteStructDecl(HirStructDecl node)
	{
		if (node.Type is HirGenericStructType genericStructType)
		{
			if (_collection.ConcretesMap.TryGetValue(genericStructType, out var concretes))
			{
				Debug.Assert(concretes.Count != 0);
				var monomorphedStructDecls = new List<HirStructDecl>(capacity: concretes.Count);
				foreach (var concrete in concretes)
				{
					var closed = _interner.GetClosedStructType(genericStructType, concrete.Types);
					monomorphedStructDecls.Add(new HirStructDecl(node.SyntaxNode, closed));
				}
				return monomorphedStructDecls;
			}
			else
			{
				// REVIEW: Unused generic type.
				return [];
			}
		}

		return [node];
	}

	private List<HirFnDecl> RewriteFnDecl(HirFnDecl node)
	{
		if (node.FnValue is HirGenericFnValue genericFnValue)
		{
			if (_collection.ConcretesMap.TryGetValue(genericFnValue, out var concretes))
			{
				Debug.Assert(concretes.Count != 0);
				var monomorphedFnDecls = new List<HirFnDecl>(capacity: concretes.Count);
				foreach (var concrete in concretes)
				{
					var closed = _interner.GetClosedFnValue(genericFnValue, concrete.Types);
					var smap = new FlatSubstitutionMap();
					smap.AddGenericSubstitutions(genericFnValue.GenericParams, concrete.Types);
					for (var i = 0; i < genericFnValue.Params.Count; i++)
					{
						smap.Add(genericFnValue.Params[i], closed.Params[i]);
					}

					monomorphedFnDecls.Add(RewriteFn(node, closed, smap));
				}
				return monomorphedFnDecls;
			}
			else
			{
				// REVIEW: Unused generic fn.
				return [];
			}
		}

		return [node];
	}

	private HirFnDecl RewriteFn(HirFnDecl fnDecl, HirClosedGenericFnValue mono, FlatSubstitutionMap smap)
	{
		Debug.Assert(fnDecl.FnValue is HirGenericFnValue);

		// Body can be null in the case of intrinsics as an e.g.
		var body = fnDecl.Body == null ? null : RewriteFnBody(fnDecl, smap);
		return new HirFnDecl(
			fnDecl.SyntaxNode,
			mono,
			body);
	}

	private HirFnBody RewriteFnBody(HirFnDecl fnDecl, FlatSubstitutionMap smap)
	{
		Debug.Assert(fnDecl.Body != null);
		return new HirFnBody(fnDecl.Body.SyntaxNode, fnDecl.Body.Stmts.Select(x => RewriteStmt(x, smap)));
	}

	[return: NotNullIfNotNull(nameof(stmt))]
	private HirStmt? RewriteStmt(HirStmt? stmt, FlatSubstitutionMap smap)
	{
		if (stmt == null)
			return null;

		if (stmt is HirLocalDeclareStmt localDeclareStmt)
		{
			var value = localDeclareStmt.Value;
			var replacedValue = new HirLocalOrParamValue(
				HirValueKind.Local, value.Identifier, value.Name, value.FullName,
				ProperlyReplace(value.Type, smap));
			smap.Add(value, replacedValue);
			return new HirLocalDeclareStmt(
				localDeclareStmt.SyntaxNode,
				replacedValue);
		}
		else if (stmt is HirValueStoreStmt valueStoreStmt)
		{
			return new HirValueStoreStmt(
				stmt.SyntaxNode,
				ProperlyReplace(valueStoreStmt.Value, smap),
				RewriteExpr(valueStoreStmt.Expr, smap));
		}
		else if (stmt is HirFieldStoreStmt fieldStoreStmt)
		{
			var replacedFieldStruct = ProperlyReplace(fieldStoreStmt.FieldValue.StructType, smap);
			return new HirFieldStoreStmt(
				stmt.SyntaxNode,
				RewriteExpr(fieldStoreStmt.Target, smap),
				ProperlyReplace(fieldStoreStmt.FieldValue, replacedFieldStruct),
				RewriteExpr(fieldStoreStmt.Expr, smap));
		}
		else if (stmt is HirReturnStmt returnStmt)
		{
			return new HirReturnStmt(
				stmt.SyntaxNode,
				RewriteExpr(returnStmt.Expr, smap));
		}
		else if (stmt is HirExprStmt exprStmt)
		{
			return new HirExprStmt(
				stmt.SyntaxNode,
				RewriteExpr(exprStmt.Expr, smap));
		}

		throw new UnreachableException();
	}

	[return: NotNullIfNotNull(nameof(expr))]
	private HirExpr? RewriteExpr(HirExpr? expr, FlatSubstitutionMap smap)
	{
		if (expr == null)
			return null;

		if (expr is HirBlockExpr blockExpr)
		{
			return new HirBlockExpr(
				expr.SyntaxNode,
				ProperlyReplace(expr.Type, smap),
				blockExpr.Stmts.Select(x => RewriteStmt(x, smap)).ToList(),
				RewriteExpr(blockExpr.BlockReturnExpr, smap));
		}
		else if (expr is HirValueLoadExpr valueLoadExpr)
		{
			return new HirValueLoadExpr(
				expr.SyntaxNode,
				ProperlyReplace(expr.Type, smap),
				ProperlyReplace(valueLoadExpr.Value, smap));
		}
		else if (expr is HirFieldLoadExpr fieldLoadExpr)
		{
			var replacedFieldStruct = ProperlyReplace(fieldLoadExpr.FieldValue.StructType, smap);
			return new HirFieldLoadExpr(
				expr.SyntaxNode,
				ProperlyReplace(expr.Type, smap),
				RewriteExpr(fieldLoadExpr.Target, smap),
				ProperlyReplace(fieldLoadExpr.FieldValue, replacedFieldStruct));
		}
		else if (expr is HirUnaryExpr unaryExpr)
		{
			return new HirUnaryExpr(
				expr.SyntaxNode,
				ProperlyReplace(expr.Type, smap),
				RewriteExpr(unaryExpr.Expr, smap),
				unaryExpr.Op);
		}
		else if (expr is HirBinaryExpr binaryExpr)
		{
			return new HirBinaryExpr(
				expr.SyntaxNode,
				ProperlyReplace(expr.Type, smap),
				RewriteExpr(binaryExpr.Left, smap),
				RewriteExpr(binaryExpr.Right, smap));
		}
		else if (expr is HirCallExpr callExpr)
		{
			return new HirCallExpr(
				expr.SyntaxNode,
				(HirFnType)ProperlyReplace(callExpr.FnType, smap),
				RewriteExpr(callExpr.Callee, smap),
				callExpr.Args.Select(x => RewriteExpr(x, smap)).ToList());
		}
		else if (expr is HirLiteralExpr literalExpr)
		{
			// REVIEW:
			return literalExpr;
		}

		throw new UnreachableException();
	}

	private HirFieldValue ProperlyReplace(HirFieldValue o, HirStructType replacedFieldStruct)
	{
		return replacedFieldStruct.Fields.First(x => x.FieldIndex == o.FieldIndex);
	}

	private HirValue ProperlyReplace(HirValue o, FlatSubstitutionMap smap)
	{
		Debug.Assert(o is not HirFieldValue);
		return smap.Replace(o);
	}

	private HirStructType ProperlyReplace(HirStructType o, FlatSubstitutionMap smap) => (HirStructType)ProperlyReplace((HirType)o, smap);

	private HirType ProperlyReplace(HirType o, FlatSubstitutionMap smap)
	{
		return _interner.EnsureReplaceTypeDeep(o, smap);
	}
}
