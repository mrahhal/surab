using Microsoft.VisualBasic;

namespace Surab.Analysis;

public class ReasonerTests
{
	[Fact]
	public void CreatesSymbols()
	{
		var src = """
			const a = 2;
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(src);

		var decl = tree.Root.DescendantNodes().OfType<ConstDeclNode>().Single();
		reasoner.TryGetDeclaredSymbol(decl, out var symbol).Should().BeTrue();
		symbol.Should().BeOfType<ConstSymbol>();
	}

	[Fact]
	public void CreatesScopes()
	{
		var src = """
			const a = 2;
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(src);

		reasoner.TryGetScope(tree.Root, out var symbol).Should().BeTrue();
		symbol.Should().BeOfType<FileScope>();
	}

	[Fact]
	public void LookupSymbol()
	{
		var src = """
			const a = 2;
			const b = a;
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(src);

		var decl1 = tree.Root.DescendantNodes().OfType<ConstDeclNode>().First();
		var decl2 = tree.Root.DescendantNodes().OfType<ConstDeclNode>().Skip(1).Single();
		var valueSymbol = reasoner.LookupSymbol(decl2.Initializer!.Expr);
		reasoner.TryGetDeclaredSymbol(decl1, out var symbol).Should().BeTrue();

		valueSymbol.Should().NotBeNull().And.Be(symbol);
	}

	[Fact]
	public void LookupSymbol_Field()
	{
		var src = """
			struct S1 { a: i32 = 42; }
			fn f() { var s = S1 { }; var a = s.a; }
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(src);

		var varStmt = tree.Root.DescendantNodes().OfType<VarStmtNode>().Skip(1).Single();
		var accessExpr = varStmt.Initializer!.Expr;
		var fieldSymbol = reasoner.LookupSymbol(accessExpr);

		fieldSymbol.Should().NotBeNull().And.BeAssignableTo<FieldSymbol>();
	}

	[Fact]
	public void LookupSymbol_LookupType_Field_InsideMapInitializer()
	{
		var src = """
			struct S1 { a: i32; }
			fn f() { var s = S1 { a = 42 }; }
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(src);

		var varStmt = tree.Root.DescendantNodes().OfType<VarStmtNode>().Single();
		var structCreationExpr = (StructCreationExprNode)varStmt.Initializer!.Expr;
		var mapInitializer = structCreationExpr.MapInitializer;

		var fieldSymbol = reasoner.LookupSymbol(mapInitializer.WellFormedExprs.ElementAt(0).Lhs);
		var fieldType = reasoner.LookupType(mapInitializer.WellFormedExprs.ElementAt(0).Lhs);

		fieldSymbol.Should().NotBeNull().And.BeAssignableTo<FieldSymbol>();
		fieldType.Should().NotBeNull();
		fieldType.KnownTypeTag.Should().Be(KnownTypeTag.i32);
	}

	[Fact]
	public void LookupSymbol_FieldInCreator()
	{
		var src = """
			struct S1 { a: i32; }
			fn f() { var s = S1 { a = 2 }; }
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(src);

		var creationExpr = tree.Root.DescendantNodes().OfType<StructCreationExprNode>().Single();
		var assignmentExpr = creationExpr.MapInitializer.DescendantNodes().OfType<AssignmentExprNode>().Single();
		var symbol = reasoner.LookupSymbol(assignmentExpr.Lhs);
		symbol.Should().BeAssignableTo<FieldSymbol>().Subject.
			Type.KnownTypeTag.Should().Be(KnownTypeTag.i32);
	}

	[Fact]
	public void LookupSymbol_InitializerParamInCreator()
	{
		var src = """
			struct S1 { (a: i32) { } }
			fn f() { var s = S1 { a = 2 }; }
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(src);

		var creationExpr = tree.Root.DescendantNodes().OfType<StructCreationExprNode>().Single();
		var assignmentExpr = creationExpr.MapInitializer.DescendantNodes().OfType<AssignmentExprNode>().Single();
		var symbol = reasoner.LookupSymbol(assignmentExpr.Lhs);
		symbol.Should().BeOfType<ParamSymbol>().Subject.
			Type.KnownTypeTag.Should().Be(KnownTypeTag.i32);
	}

	[Fact]
	public void LookupType()
	{
		var src = """
			const a = 2;
			const c = '2';
			const b = "2";
			const d = true;
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(src);

		var decl1 = tree.Root.DescendantNodes().OfType<ConstDeclNode>().ElementAt(0);
		var decl2 = tree.Root.DescendantNodes().OfType<ConstDeclNode>().ElementAt(1);
		var decl3 = tree.Root.DescendantNodes().OfType<ConstDeclNode>().ElementAt(2);
		var decl4 = tree.Root.DescendantNodes().OfType<ConstDeclNode>().ElementAt(3);

		reasoner.LookupType(decl1.Initializer!.Expr).KnownTypeTag.Should().Be(KnownTypeTag.i32);
		reasoner.LookupType(decl2.Initializer!.Expr).KnownTypeTag.Should().Be(KnownTypeTag.@char);
		reasoner.LookupType(decl3.Initializer!.Expr).KnownTypeTag.Should().Be(KnownTypeTag.@string);
		reasoner.LookupType(decl4.Initializer!.Expr).KnownTypeTag.Should().Be(KnownTypeTag.@bool);
	}

	[Fact]
	public void LookupType_Block()
	{
		var src = """
			fn f() {
				var a = { 2 };
			}
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(src);

		var letStmt = tree.Root.DescendantNodes().OfType<VarStmtNode>().Single();
		var initializerSymbol = reasoner.LookupType(letStmt.Initializer!);
		initializerSymbol.KnownTypeTag.Should().Be(KnownTypeTag.i32);
	}

	[Fact]
	public void Symbol_Alias()
	{
		var src = """
			alias Integer as i32;
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(src);

		var aliasDecl = tree.Root.DescendantNodes().OfType<AliasDeclNode>().Single();
		reasoner.TryGetDeclaredSymbol(aliasDecl, out var symbol).Should().BeTrue();
		symbol.Should().BeOfType<AliasTypeSymbol>();
	}

	[Fact]
	public void Symbol_Alias_UnwrapAliases()
	{
		var src = """
			alias Integer as i32;
			alias AnotherInteger as Integer;
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(src);

		var anotherIntegerAliasDecl = tree.Root.DescendantNodes().OfType<AliasDeclNode>().ElementAt(1);
		var symbol = (AliasTypeSymbol)reasoner.GetDeclaredSymbol(anotherIntegerAliasDecl)!;
		symbol.UnwrapAliases().KnownTypeTag.Should().Be(KnownTypeTag.i32);
	}

	[Fact]
	public void Symbol_IsAssignableFrom()
	{
		var src = """
			alias Integer as i32;
			alias AnotherInteger as Integer;
			""";
		var (project, tree, reasoner) = AnalysisTestUtils.CreateProjectAndTreeAndReasoner(src);

		var anotherIntegerAliasDecl = tree.Root.DescendantNodes().OfType<AliasDeclNode>().ElementAt(1);
		var symbol = (AliasTypeSymbol)reasoner.GetDeclaredSymbol(anotherIntegerAliasDecl)!;
		symbol.IsAssignableFrom(project.ResolveKnownTypeSymbol(KnownTypeTag.i32));
		project.ResolveKnownTypeSymbol(KnownTypeTag.i32).IsAssignableFrom(symbol);
	}

	[Fact]
	public void Symbol_IsAssignableFrom2()
	{
		var src = """
			alias Delegate1 as fn (i32) f64;
			alias Delegate2 as fn (i32) f64;
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(src);

		var aliasDecl1 = tree.Root.DescendantNodes().OfType<AliasDeclNode>().ElementAt(0);
		var aliasDecl2 = tree.Root.DescendantNodes().OfType<AliasDeclNode>().ElementAt(1);
		var symbol1 = (AliasTypeSymbol)reasoner.GetDeclaredSymbol(aliasDecl1)!;
		var symbol2 = (AliasTypeSymbol)reasoner.GetDeclaredSymbol(aliasDecl2)!;
		symbol1.IsAssignableFrom(symbol2);
	}

	[Fact]
	public void LookupType_Call()
	{
		var src = """
			fn f1() i32 { return 2; }

			fn f2() {
				f1();
			}
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(src);

		var callExpr = tree.Root.DescendantNodes().OfType<CallExprNode>().Single();
		var callType = reasoner.LookupType(callExpr);
		callType.KnownTypeTag.Should().Be(KnownTypeTag.i32);
	}

	[Fact]
	public void LookupType_Call_FnPtr()
	{
		var src = """
			fn f1() i32 { return 2; }

			fn f2() {
				var f = f1;
				f();
			}
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(src);

		var callExpr = tree.Root.DescendantNodes().OfType<CallExprNode>().Single();
		var callType = reasoner.LookupType(callExpr);
		callType.KnownTypeTag.Should().Be(KnownTypeTag.i32);
	}

	[Fact]
	public void LookupType_Field()
	{
		var src = """
			struct S1 { a: i32 = 42; }
			fn f() { var s = S1 { }; var a = s.a; }
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(src);

		var varStmt = tree.Root.DescendantNodes().OfType<VarStmtNode>().Skip(1).Single();
		var accessExpr = varStmt.Initializer!.Expr;
		var fieldType = reasoner.LookupType(accessExpr);
		fieldType.Should().NotBeNull();
		fieldType.KnownTypeTag.Should().Be(KnownTypeTag.i32);
	}

	[Fact]
	public void LookupType_NumericExplicitType()
	{
		var src = """
			fn f() { var n = 32f32; }
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(src);

		var varStmt = tree.Root.DescendantNodes().OfType<VarStmtNode>().Single();
		var literalExpr = varStmt.Initializer!.Expr;
		var numericType = reasoner.LookupType(literalExpr);
		numericType.KnownTypeTag.Should().Be(KnownTypeTag.f32);
	}

	[Fact]
	public void LookupType_PtrType()
	{
		var src = """
			fn f() {
				var a: i32 = 2;
				var b = &a;
				var c = *b;
			}
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(src);

		var aType = ((LocalSymbol)reasoner.GetDeclaredSymbol(tree.Root.DescendantNodes().OfType<VarStmtNode>().ElementAt(0))!).Type;
		var bType = ((LocalSymbol)reasoner.GetDeclaredSymbol(tree.Root.DescendantNodes().OfType<VarStmtNode>().ElementAt(1))!).Type;
		var cType = ((LocalSymbol)reasoner.GetDeclaredSymbol(tree.Root.DescendantNodes().OfType<VarStmtNode>().ElementAt(2))!).Type;

		aType.KnownTypeTag.Should().Be(KnownTypeTag.i32);
		bType.Should().BeOfType<PtrTypeSymbol>().Subject.InnerType!.KnownTypeTag.Should().Be(KnownTypeTag.i32);
		cType.KnownTypeTag.Should().Be(KnownTypeTag.i32);
	}

	[Fact]
	public void LookupType_PtrType_AutoDeref()
	{
		var src = """
			struct S {
				get() i32 { 42 }
			}

			fn f() {
				var s = S { };
				var a = s.get();
				var s_ptr = &s;
				// Pass through type analysis for pointers. This should properly resolve the method.
				var b = s_ptr.get();
			}
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(src);

		var aType = ((LocalSymbol)reasoner.GetDeclaredSymbol(tree.Root.DescendantNodes().OfType<VarStmtNode>().ElementAt(1))!).Type;
		var bType = ((LocalSymbol)reasoner.GetDeclaredSymbol(tree.Root.DescendantNodes().OfType<VarStmtNode>().ElementAt(3))!).Type;

		aType.KnownTypeTag.Should().Be(KnownTypeTag.i32);
		bType.KnownTypeTag.Should().Be(KnownTypeTag.i32);
	}

	[Fact]
	public void Symbol_UnknownType()
	{
		var src = """
			fn f() {
				var a: Unknown = 2;
			}
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(src);

		var letStmt = tree.Root.DescendantNodes().OfType<VarStmtNode>().Single();
		reasoner.TryGetDeclaredSymbol(letStmt, out var symbol).Should().BeTrue();
		var localSymbol = (LocalSymbol)symbol!;
		localSymbol.Type.Should().BeOfType<UnknownTypeSymbol>();
		localSymbol.IsTypeInferred.Should().BeFalse();
	}

	[Fact]
	public void Symbol_InferType_NumericInteger_i32()
	{
		ExpectLocalInferredTypeFromExpr("2", KnownTypeTag.i32);
	}

	[Fact]
	public void Symbol_InferType_NumericFloat_f64()
	{
		ExpectLocalInferredTypeFromExpr("2.0", KnownTypeTag.f64);
	}

	[Fact]
	public void Symbol_InferType_Block()
	{
		ExpectLocalInferredTypeFromExpr("{ 2 }", KnownTypeTag.i32);
	}

	private void ExpectLocalInferredTypeFromExpr(string expr, KnownTypeTag expectedTypeTag)
	{
		var src = $$"""
			fn f() {
				var a = {{expr}};
			}
			""";
		ExpectLocalInferredType(src, expectedTypeTag);
	}

	private void ExpectLocalInferredType(string src, KnownTypeTag expectedTypeTag)
	{
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(src);

		var letStmt = tree.Root.DescendantNodes().OfType<VarStmtNode>().Single();
		reasoner.TryGetDeclaredSymbol(letStmt, out var symbol).Should().BeTrue();
		var localSymbol = symbol.Should().BeOfType<LocalSymbol>().Subject;
		localSymbol.Type.KnownTypeTag.Should().Be(expectedTypeTag);
		localSymbol.IsTypeInferred.Should().BeTrue();
	}

	[Fact]
	public void Symbol_InferType_Field()
	{
		var src = """
			struct S1 {
				a = true;
			}
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(src);

		var fieldDecl = tree.Root.DescendantNodes().OfType<FieldDeclNode>().Single();
		reasoner.TryGetDeclaredSymbol(fieldDecl, out var symbol).Should().BeTrue();
		var fieldSymbol = (FieldSymbol)symbol!;
		fieldSymbol.Type.KnownTypeTag.Should().Be(KnownTypeTag.@bool);
		fieldSymbol.IsTypeInferred.Should().BeTrue();
	}

	[Fact]
	public void FnSymbol_ReturnTypeAsNoneIfNotSet()
	{
		var src = """
			fn f() { }
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(src);

		var fnDecl = tree.Root.DescendantNodes().OfType<FnDeclNode>().Single();
		reasoner.TryGetDeclaredSymbol(fnDecl, out var symbol).Should().BeTrue();
		var fnSymbol = symbol.Should().BeOfType<FnSymbol>().Subject;
		fnSymbol.ReturnType.KnownTypeTag.Should().Be(KnownTypeTag.None);
	}

	[Fact]
	public void PtrTypes()
	{
		var src = """
			alias Integer as i32;

			fn f() {
				var a1: *i32;
				var a2: *i32;
				var a3: *Integer;
				var a4: *;
				var a5: *;
			}
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(src);

		var a1Type = GetTypeAt(0);
		var a2Type = GetTypeAt(1);
		var a3Type = GetTypeAt(2);
		var a4Type = GetTypeAt(3);
		var a5Type = GetTypeAt(4);

		a1Type.Equals(a2Type).Should().BeTrue();
		a1Type.IsAssignableFrom(a2Type).Should().BeTrue();
		a1Type.Equals(a3Type).Should().BeFalse();
		a1Type.IsAssignableFrom(a3Type).Should().BeTrue();
		a1Type.IsAssignableFrom(a4Type).Should().BeFalse();
		a4Type.IsAssignableFrom(a5Type).Should().BeTrue();

		TypeSymbol GetTypeAt(int index)
		{
			var a1Node = tree.Root.DescendantNodes().OfType<VarStmtNode>().ElementAt(index);
			return ((LocalSymbol)reasoner.GetDeclaredSymbol(a1Node)!).Type;
		}
	}

	[Fact]
	public void FnTypesForCompatibleFnSigsAreEqual()
	{
		var src = """
			fn f1(a: i32, b: i32) i64 { return 1.0; }
			fn f2(a: i32, b: i32) i64 { return 2.0; }
			fn f3(a: i32, b: i32) { }
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(src);

		var f1Node = tree.Root.DescendantNodes().OfType<FnDeclNode>().ElementAt(0);
		var f1Symbol = (FnSymbol)reasoner.GetDeclaredSymbol(f1Node)!;
		var f2Node = tree.Root.DescendantNodes().OfType<FnDeclNode>().ElementAt(1);
		var f2Symbol = (FnSymbol)reasoner.GetDeclaredSymbol(f2Node)!;
		var f3Node = tree.Root.DescendantNodes().OfType<FnDeclNode>().ElementAt(2);
		var f3Symbol = (FnSymbol)reasoner.GetDeclaredSymbol(f3Node)!;

		f1Symbol.Type.Equals(f2Symbol.Type).Should().BeTrue();
		f1Symbol.Type.Equals(f3Symbol.Type).Should().BeFalse();
	}

	[Fact]
	public void FnTypesWithDifferringExternAreNotEqual()
	{
		var src = """
			@extern fn f1(a: i32, b: i32) i64 { return 1.0; }
			@extern fn f2(a: i32, b: i32) i64 { return 2.0; }
			fn f3(a: i32, b: i32) i64 { return 3.0; }
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(src);

		var f1Node = tree.Root.DescendantNodes().OfType<FnDeclNode>().ElementAt(0);
		var f1Symbol = (FnSymbol)reasoner.GetDeclaredSymbol(f1Node)!;
		var f2Node = tree.Root.DescendantNodes().OfType<FnDeclNode>().ElementAt(1);
		var f2Symbol = (FnSymbol)reasoner.GetDeclaredSymbol(f2Node)!;
		var f3Node = tree.Root.DescendantNodes().OfType<FnDeclNode>().ElementAt(2);
		var f3Symbol = (FnSymbol)reasoner.GetDeclaredSymbol(f3Node)!;

		f1Symbol.Type.Equals(f2Symbol.Type).Should().BeTrue();
		f1Symbol.Type.Equals(f3Symbol.Type).Should().BeFalse();
	}

	[Fact]
	public void FnTypesForCompatibleFnTypesAreEqual()
	{
		var src = """
			fn f() {
				var f1: fn (i32, i32) i64;
				var f2: fn (i32, i32) i64;
				var f3: fn (i32, i32);
			}
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(src);

		var f1Node = tree.Root.DescendantNodes().OfType<VarStmtNode>().ElementAt(0);
		var f1Symbol = (LocalSymbol)reasoner.GetDeclaredSymbol(f1Node)!;
		var f2Node = tree.Root.DescendantNodes().OfType<VarStmtNode>().ElementAt(1);
		var f2Symbol = (LocalSymbol)reasoner.GetDeclaredSymbol(f2Node)!;
		var f3Node = tree.Root.DescendantNodes().OfType<VarStmtNode>().ElementAt(2);
		var f3Symbol = (LocalSymbol)reasoner.GetDeclaredSymbol(f3Node)!;

		f1Symbol.Type.Should().BeOfType<FnTypeSymbol>();
		f1Symbol.Type.Equals(f2Symbol.Type).Should().BeTrue();
		f1Symbol.Type.Equals(f3Symbol.Type).Should().BeFalse();
	}

	[Fact]
	public void FnTypesUsingTypeAliases()
	{
		var src = """
			alias Integer1 i32;
			alias Integer2 i32;
			alias Float f64;

			fn f() {
				var f1: fn (Integer1, i32, Integer1) f64;
				var f2: fn (i32, Integer2, Integer2) Float;
			}
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(src);

		var f1Node = tree.Root.DescendantNodes().OfType<VarStmtNode>().ElementAt(0);
		var f1Symbol = (LocalSymbol)reasoner.GetDeclaredSymbol(f1Node)!;
		var f2Node = tree.Root.DescendantNodes().OfType<VarStmtNode>().ElementAt(1);
		var f2Symbol = (LocalSymbol)reasoner.GetDeclaredSymbol(f2Node)!;

		f1Symbol.Type.IsAssignableFrom(f2Symbol.Type).Should().BeTrue();
		f2Symbol.Type.IsAssignableFrom(f1Symbol.Type).Should().BeTrue();
	}

	[Fact]
	public void FnTypes_FnTypesWithErrorsAreNotCompatible()
	{
		var src = """
			fn f1(a: Unknown) { }
			fn f2(a: Unknown) { }
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(src);

		var fnDeclNode1 = tree.Root.DescendantNodes().OfType<FnDeclNode>().ElementAt(0);
		var fnSymbol1 = (FnSymbol)reasoner.GetDeclaredSymbol(fnDeclNode1)!;
		var fnDeclNode2 = tree.Root.DescendantNodes().OfType<FnDeclNode>().ElementAt(1);
		var fnSymbol2 = (FnSymbol)reasoner.GetDeclaredSymbol(fnDeclNode2)!;

		fnSymbol1.Type.IsAssignableFrom(fnSymbol2.Type).Should().BeFalse();
	}

	[Fact]
	public void FnSymbol_IsExtern()
	{
		var src = """
			@extern fn f();
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(src);

		var fnDecl = tree.Root.DescendantNodes().OfType<FnDeclNode>().Single();
		reasoner.TryGetDeclaredSymbol(fnDecl, out var symbol).Should().BeTrue();
		var fnSymbol = symbol.Should().BeOfType<FnSymbol>().Subject;
		fnSymbol.IsExtern.Should().BeTrue();
	}

	[Fact]
	public void FnSymbol_OuterFn()
	{
		var src = """
			struct S1 { }
			fn f() { }
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(src);

		var fnDecl = tree.Root.DescendantNodes().OfType<FnDeclNode>().Single();
		reasoner.TryGetDeclaredSymbol(fnDecl, out var symbol).Should().BeTrue();
		var fnSymbol = symbol.Should().BeOfType<FnSymbol>().Subject;
		fnSymbol.IsOuterFn.Should().BeTrue();
	}

	[Fact]
	public void FnSymbol_TypeWithoutReceiver()
	{
		var src = """
			struct S1 { a() { } }
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(src);

		var methodDecl = tree.Root.DescendantNodes().OfType<MethodDeclNode>().Single();
		reasoner.TryGetDeclaredSymbol(methodDecl, out var symbol).Should().BeTrue();
		var fnSymbol = symbol.Should().BeOfType<FnSymbol>().Subject;
		fnSymbol.Type.ParamTypes.Should().HaveCount(1); // reciever
		fnSymbol.TypeWithoutReceiver.ParamTypes.Should().HaveCount(0); // no receiver
	}
}
