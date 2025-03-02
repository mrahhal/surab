namespace Surab.Analysis;

public class ReasonerTests
{
	[Fact]
	public void CreatesSymbols()
	{
		var code = """
			const a = 2;
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

		var decl = tree.Root.DescendantNodes().OfType<ConstDeclNode>().Single();
		reasoner.TryGetDeclaredSymbol(decl, out var symbol).Should().BeTrue();
		symbol.Should().BeAssignableTo<ConstSymbol>();
	}

	[Fact]
	public void CreatesScopes()
	{
		var code = """
			const a = 2;
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

		reasoner.TryGetScope(tree.Root, out var symbol).Should().BeTrue();
		symbol.Should().BeOfType<FileScope>();
	}

	[Fact]
	public void LookupSymbol()
	{
		var result = AnalysisTestUtils.CreateFromNodeAnnotatedCode("""
			{|a:|}const a = 2;
			{|b:|}const b = a;
			""");
		var reasoner = result.Reasoner;

		var decl1 = result.GetNode<ConstDeclNode>("a");
		var decl2 = result.GetNode<ConstDeclNode>("b");
		var valueSymbol = reasoner.LookupSymbol(decl2.Initializer!.Expr);
		reasoner.TryGetDeclaredSymbol(decl1, out var symbol).Should().BeTrue();

		valueSymbol.Should().NotBeNull().And.Be(symbol);
	}

	[Fact]
	public void LookupSymbol_Field()
	{
		var result = AnalysisTestUtils.CreateFromNodeAnnotatedCode("""
			struct S1 { a: i32 = 42; }
			fn f() { {|var_s:|}var s = S1.{ }; {|var_a:|}var a = s.a; }
			""");
		var reasoner = result.Reasoner;

		var var_a = result.GetNode<VarDeclNode>("var_a");
		var accessExpr = (AccessExprNode)var_a.Initializer!.Expr;
		var fieldSymbol = reasoner.LookupSymbol(accessExpr);
		// Also ensure lookup on name works the same.
		var fieldSymbol2 = reasoner.LookupSymbol(accessExpr.Name);

		fieldSymbol.Should().NotBeNull().And.BeAssignableTo<FieldSymbol>();
		fieldSymbol.Should().Be(fieldSymbol2);
	}

	[Fact]
	public void LookupSymbol_CannotAccessSymbolBeforeDeclaration()
	{
		var code = """
			fn f() {
				print_i32(a);
				var a = 2;
			}
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

		var callExpr = tree.Root.DescendantNodes().OfType<CallExprNode>().Single();
		var symbol = reasoner.LookupSymbol(callExpr.Args.ElementAt(0).Expr);
		symbol.Should().BeNull();
	}

	[Fact]
	public void TypeCreation_Nested()
	{
		var code = """
			struct S {
				a: i32;
				struct SNested {
					b: i32;
				}
			}
			fn f() {
				var s_nested = S.SNested.{ b: 42 };
			}
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

		var creationExpr = (TypeCreationExprNode)tree.Root.DescendantNodes().OfType<VarDeclNode>().Single().Initializer!.Expr;

		var creationExprType = reasoner.LookupType(creationExpr);

		creationExprType.Should().BeAssignableTo<NamedTypeSymbol>().Subject.Name.Should().Be("SNested");
	}

	[Fact]
	public void IfExpr1()
	{
		var result = AnalysisTestUtils.CreateFromNodeAnnotatedCode("""
			fn f() {
				{|if1:|}if (true) {
				} else {
				}

				{|a:|}var a = if (true) {
					42
				} else {
					43
				};
			}
			""");
		var reasoner = result.Reasoner;

		var ifExpr = result.GetNode<IfExprNode>("if1");
		reasoner.LookupType(ifExpr).KnownTypeTag.Should().Be(KnownTypeTag.@void);

		var a_var_decl = result.GetNode<VarDeclNode>("a");
		reasoner.GetDeclaredSymbol(a_var_decl).Type.KnownTypeTag.Should().Be(KnownTypeTag.i32);
	}

	[Fact]
	public void Field_InsideObjectInitializer()
	{
		var code = """
			struct S1 { a: i32; }
			fn f() { var s = S1.{ a: 42 }; }
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

		var varDecl = tree.Root.DescendantNodes().OfType<VarDeclNode>().Single();
		var creationExpr = (TypeCreationExprNode)varDecl.Initializer!.Expr;
		var objectInitializer = creationExpr.ObjectInitializer;

		var fieldSymbol = reasoner.LookupSymbol(objectInitializer.Items.ElementAt(0));
		var fieldType = reasoner.LookupType(objectInitializer.Items.ElementAt(0));

		fieldSymbol.Should().NotBeNull().And.BeAssignableTo<FieldSymbol>();
		fieldType.Should().NotBeNull();
		fieldType.KnownTypeTag.Should().Be(KnownTypeTag.i32);
	}

	[Fact]
	public void LookupSymbol_FieldInCreator()
	{
		var code = """
			struct S1 { a: i32; }
			fn f() { var s = S1.{ a: 2 }; }
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

		var creationExpr = tree.Root.DescendantNodes().OfType<TypeCreationExprNode>().Single();
		var symbol = reasoner.LookupSymbol(creationExpr.ObjectInitializer.Items.ElementAt(0));
		symbol.Should().BeAssignableTo<FieldSymbol>().Subject.
			Type.KnownTypeTag.Should().Be(KnownTypeTag.i32);
	}

	[Fact]
	public void LookupSymbol_InitializerParamInCreator()
	{
		var code = """
			struct S1 { (a: i32) { } }
			fn f() { var s = S1.{ a: 2 }; }
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

		var creationExpr = tree.Root.DescendantNodes().OfType<TypeCreationExprNode>().Single();
		var symbol = reasoner.LookupSymbol(creationExpr.ObjectInitializer.Items.ElementAt(0));
		symbol.Should().BeAssignableTo<ParamSymbol>().Subject.
			Type.KnownTypeTag.Should().Be(KnownTypeTag.i32);
	}

	[Fact]
	public void LookupType()
	{
		var code = """
			const a = 2;
			const c = '2';
			const b = "2";
			const d = true;
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

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
		var code = """
			fn f() {
				var a = { 2 };
			}
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

		var varDecl = tree.Root.DescendantNodes().OfType<VarDeclNode>().Single();
		var initializerSymbol = reasoner.LookupType(varDecl.Initializer!);
		initializerSymbol.KnownTypeTag.Should().Be(KnownTypeTag.i32);
	}

	[Fact]
	public void Symbol_Alias()
	{
		var code = """
			alias i32 as Integer;
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

		var aliasDecl = tree.Root.DescendantNodes().OfType<AliasDeclNode>().Single();
		reasoner.TryGetDeclaredSymbol(aliasDecl, out var symbol).Should().BeTrue();
		symbol.Should().BeAssignableTo<AliasTypeSymbol>();
	}

	[Fact]
	public void Symbol_Alias_UnwrapAliases()
	{
		var code = """
			alias i32 as Integer;
			alias Integer as AnotherInteger;
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

		var anotherIntegerAliasDecl = tree.Root.DescendantNodes().OfType<AliasDeclNode>().ElementAt(1);
		var symbol = reasoner.GetDeclaredSymbol(anotherIntegerAliasDecl)!;
		symbol.UnwrapTypeAliases().KnownTypeTag.Should().Be(KnownTypeTag.i32);
	}

	[Fact]
	public void Symbol_Alias_IsAssignableFrom()
	{
		var code = """
			alias i32 as Integer;
			alias Integer as AnotherInteger;
			""";
		var (project, tree, reasoner) = AnalysisTestUtils.CreateProjectAndTreeAndReasoner(code);

		var anotherIntegerAliasDecl = tree.Root.DescendantNodes().OfType<AliasDeclNode>().ElementAt(1);
		var symbol = reasoner.GetDeclaredSymbol(anotherIntegerAliasDecl)!;
		symbol.IsAssignableFrom(project.Workspace.ResolveKnownTypeSymbol(KnownTypeTag.i32));
		project.Workspace.ResolveKnownTypeSymbol(KnownTypeTag.i32).IsAssignableFrom(symbol);
	}

	[Fact]
	public void Symbol_Alias_IsAssignableFrom2()
	{
		var code = """
			alias fn (i32) f64 as Delegate1;
			alias fn (i32) f64 as Delegate2;
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

		var aliasDecl1 = tree.Root.DescendantNodes().OfType<AliasDeclNode>().ElementAt(0);
		var aliasDecl2 = tree.Root.DescendantNodes().OfType<AliasDeclNode>().ElementAt(1);
		var symbol1 = reasoner.GetDeclaredSymbol(aliasDecl1)!;
		var symbol2 = reasoner.GetDeclaredSymbol(aliasDecl2)!;
		symbol1.IsAssignableFrom(symbol2);
	}

	[Fact]
	public void Symbol_Alias_TypeCreationMemberSymbol()
	{
		var code = """
			struct S { v: i32; }
			alias S as SAlias;

			fn fn() {
				var s = SAlias.{ v: 42 };
			}
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

		var objectInitializerItem = tree.Root.DescendantNodes().OfType<ObjectInitializerItemNode>().First();
		var result = reasoner.LookupSymbol(objectInitializerItem);
		result.Should().BeAssignableTo<FieldSymbol>();
	}

	[Fact]
	public void Generics()
	{
		var code = """
			struct S<T1> {
				struct N<T2> { b: T2; m: M<i32, i64>; struct NN<T3> { } }
				struct M<T3, T4> { c1: T3; c2: T4; }
				struct O { o1: T1; o2: i32; }

				a: T1;
				n: N<f64>;
				nn: N<f64>.NN<i32>;
				m: M<f64, T1>;
				o: O;
			}

			fn f() {
				var s = S<i32>.{ a: 42 };
				var a = s.a;
				var n = s.n;
				var nn = s.nn;
				var m = s.m;
				var o = s.o;

				var x = S<i32>.N<i64>.{ b: 43i64 };

				var b = S<i32>.M<bool, bool>.{ c1: false, c2: true };
			}
			""";

		var (project, tree, reasoner) = AnalysisTestUtils.CreateProjectAndTreeAndReasoner(code);

		var SDecl = tree.Root.DescendantNodes().OfType<StructDeclNode>().ElementAt(0);
		var SSymbol = reasoner.GetDeclaredSymbol(SDecl);
		SSymbol.GenericParams.Should().HaveCount(1);
		var NSymbol = (NamedTypeSymbol)SSymbol.GetMember("N")!;
		NSymbol.GenericParams.Should().HaveCount(1);
		var NNSymbol = NSymbol.GetMember("NN");
		var MSymbol = (NamedTypeSymbol)SSymbol.GetMember("M")!;
		MSymbol.GenericParams.Should().HaveCount(2);
		var OSymbol = (NamedTypeSymbol)SSymbol.GetMember("O")!;

		var aFieldSymbol = (FieldSymbol)SSymbol.GetMember("a")!;
		aFieldSymbol.Type.Should().Be(SSymbol.GenericParams[0]);
		var nFieldSymbol = (FieldSymbol)SSymbol.GetMember("n")!;
		nFieldSymbol.Type.OriginalDef.Should().Be(NSymbol);
		nFieldSymbol.Type.FullName.Should().Be("main::S<T1>.N<f64>");
		var nnFieldSymbol = (FieldSymbol)SSymbol.GetMember("nn")!;
		nnFieldSymbol.Type.OriginalDef.Should().Be(NNSymbol);
		nnFieldSymbol.Type.FullName.Should().Be("main::S<T1>.N<f64>.NN<i32>");
		var mFieldSymbol = (FieldSymbol)SSymbol.GetMember("m")!;
		mFieldSymbol.Type.OriginalDef.Should().Be(MSymbol);
		mFieldSymbol.Type.FullName.Should().Be("main::S<T1>.M<f64, T1>");
		var oFieldSymbol = (FieldSymbol)SSymbol.GetMember("o")!;
		oFieldSymbol.Type.OriginalDef.Should().Be(OSymbol);
		oFieldSymbol.Type.FullName.Should().Be("main::S<T1>.O");

		var expectedS1ClosedType = reasoner.CloseGeneric(SSymbol, [project.Workspace.ResolveKnownTypeSymbol(KnownTypeTag.i32)]);

		var sVarDecl = tree.Root.DescendantNodes().OfType<VarDeclNode>().ElementAt(0);
		var sLocalSymbol = reasoner.GetDeclaredSymbol(sVarDecl);
		sLocalSymbol.Type.Should().Be(expectedS1ClosedType);
		var s_aFieldSymbol = (FieldSymbol)sLocalSymbol.Type.GetMember("a")!;
		s_aFieldSymbol.Type.FullName.Should().Be("std._prelude::i32");
		var s_nFieldSymbol = (FieldSymbol)sLocalSymbol.Type.GetMember("n")!;
		s_nFieldSymbol.Type.Should().BeAssignableTo<SubstitutedNamedTypeSymbol>().Subject.OriginalDef.Should().Be(NSymbol);
		s_nFieldSymbol.Type.FullName.Should().Be("main::S<i32>.N<f64>");
		var s_nnFieldSymbol = (FieldSymbol)sLocalSymbol.Type.GetMember("nn")!;
		s_nnFieldSymbol.Type.Should().BeAssignableTo<SubstitutedNamedTypeSymbol>().Subject.OriginalDef.Should().Be(NNSymbol);
		s_nnFieldSymbol.Type.FullName.Should().Be("main::S<i32>.N<f64>.NN<i32>");
		var s_mFieldSymbol = (FieldSymbol)sLocalSymbol.Type.GetMember("m")!;
		s_mFieldSymbol.Type.Should().BeAssignableTo<SubstitutedNamedTypeSymbol>().Subject.OriginalDef.Should().Be(MSymbol);
		s_mFieldSymbol.Type.FullName.Should().Be("main::S<i32>.M<f64, i32>");

		var sTypeCreationExpr = sVarDecl.Initializer!.Expr.Should().BeOfType<TypeCreationExprNode>().Subject;
		var sTypeSymbol = reasoner.LookupSymbol(sTypeCreationExpr.TypeExpr).Should().BeAssignableTo<NamedTypeSymbol>().Subject;
		var sTypeSymbolSub = sTypeSymbol.Should().BeAssignableTo<SubstitutedNamedTypeSymbol>().Subject;
		sTypeSymbolSub.OriginalDef.Should().Be(SSymbol);
		sTypeSymbol.GenericArgs.Should().HaveCount(1);
		sTypeSymbol.GenericArgs[0].Should().Be(project.Workspace.ResolveKnownTypeSymbol(KnownTypeTag.i32));

		var aVarDecl = tree.Root.DescendantNodes().OfType<VarDeclNode>().ElementAt(1);
		var aLocalSymbol = reasoner.GetDeclaredSymbol(aVarDecl);
		aLocalSymbol.Type.Should().Be(project.Workspace.ResolveKnownTypeSymbol(KnownTypeTag.i32));

		var nVarDecl = tree.Root.DescendantNodes().OfType<VarDeclNode>().ElementAt(2);
		var nLocalSymbol = reasoner.GetDeclaredSymbol(nVarDecl);
		var nLocalType = nLocalSymbol.Type.Should().BeAssignableTo<SubstitutedNamedTypeSymbol>().Subject;
		nLocalType.GenericArgs.Should().HaveCount(1);
		nLocalType.GenericArgs[0].Should().Be(project.Workspace.ResolveKnownTypeSymbol(KnownTypeTag.f64));
		nLocalType.OriginalDef.Should().Be(NSymbol);
		nLocalType.Parent.Should().Be(sLocalSymbol.Type);
		nLocalType.FullName.Should().Be("main::S<i32>.N<f64>");

		var nnVarDecl = tree.Root.DescendantNodes().OfType<VarDeclNode>().ElementAt(3);
		var nnLocalSymbol = reasoner.GetDeclaredSymbol(nnVarDecl);
		var nnLocalType = nnLocalSymbol.Type.Should().BeAssignableTo<SubstitutedNamedTypeSymbol>().Subject;
		nnLocalType.FullName.Should().Be("main::S<i32>.N<f64>.NN<i32>");

		var oVarDecl = tree.Root.DescendantNodes().OfType<VarDeclNode>().ElementAt(5);
		var oLocalSymbol = reasoner.GetDeclaredSymbol(oVarDecl);
		var oLocalType = oLocalSymbol.Type.Should().BeAssignableTo<SubstitutedNamedTypeSymbol>().Subject;
		oLocalType.FullName.Should().Be("main::S<i32>.O");

		var bVarDecl = tree.Root.DescendantNodes().OfType<VarDeclNode>().ElementAt(7);
		var bLocalSymbol = reasoner.GetDeclaredSymbol(bVarDecl);
		var bLocalType = bLocalSymbol.Type.Should().BeAssignableTo<SubstitutedNamedTypeSymbol>().Subject;
		bLocalType.FullName.Should().Be("main::S<i32>.M<bool, bool>");
	}

	[Fact]
	public void Generics_Fns()
	{
		var result = AnalysisTestUtils.CreateFromNodeAnnotatedCode("""
			fn f1<T>(v: T) { }
			fn f2<T>(v: *T) *T { v }
			struct S<T1> { a: T1; fn bar<T2> { } }
			fn f() {
				var s = S.{ a: 42 };
				var a = s.a;
				{|f1_i32_call:|}f1<i32>(42);
				var v = 42;
				{|f2_i32_call:|}f2<i32>(&v);

				var a = 2;
				var ptr = &a;
				var ptr_2 = std.memory::{|ptr_add_offset_call:|}ptr_add_offset<i32>(ptr, 1);
			}
			""");
		var reasoner = result.Reasoner;

		var f1_i32_call = result.GetNode("f1_i32_call");
		var f2_i32_call = result.GetNode("f2_i32_call");
		var ptr_add_offset_call = result.GetNode("ptr_add_offset_call");

		var f1_i32_call_symbol = (FnSymbol)reasoner.LookupSymbol(f1_i32_call)!;
		var f2_i32_call_symbol = (FnSymbol)reasoner.LookupSymbol(f2_i32_call)!;
		var ptr_add_offset_call_symbol = (FnSymbol)reasoner.LookupSymbol(ptr_add_offset_call)!;
		var ptr_add_offset_call_type = ptr_add_offset_call_symbol.Type;

		f1_i32_call_symbol.Params[0].Type.KnownTypeTag.Should().Be(KnownTypeTag.i32);
		var innerType = f2_i32_call_symbol.Params[0].Type.Should().BeAssignableTo<PtrTypeSymbol>().Subject.InnerType;
		innerType.Should().NotBeNull();
		innerType!.KnownTypeTag.Should().Be(KnownTypeTag.i32);

		ptr_add_offset_call_type.ParamTypes[0].Should().Be(ptr_add_offset_call_symbol.Params[0].Type);
		var innerType2 = ptr_add_offset_call_symbol.Params[0].Type.Should().BeAssignableTo<PtrTypeSymbol>().Subject.InnerType;
		innerType2.Should().NotBeNull();
		innerType2.Should().Be(reasoner.Workspace.ResolveKnownTypeSymbol(KnownTypeTag.i32));
	}

	[Fact]
	public void Generics_Alias()
	{
		var code = """
			struct S<T1> { a: T1; }
			alias S<i32> as SAlias;
			alias S<T> as SAlias2<T>;
			fn f() {
				var s1 = S<i32>.{ a: 42 };
				var s2 = SAlias.{ a: 42 };
				var s3 = SAlias2<i32>.{ a: 42 };
			}
			""";
		var (project, tree, reasoner) = AnalysisTestUtils.CreateProjectAndTreeAndReasoner(code);

		var SAliasDecl = tree.Root.DescendantNodes().OfType<AliasDeclNode>().ElementAt(0);
		var SAliasSymbol = reasoner.GetDeclaredSymbol(SAliasDecl);
		SAliasSymbol.TargetType.FullName.Should().Be("main::S<i32>");
		var SAlias2Decl = tree.Root.DescendantNodes().OfType<AliasDeclNode>().ElementAt(1);
		var SAlias2Symbol = reasoner.GetDeclaredSymbol(SAlias2Decl);
		SAlias2Symbol.FullName.Should().Be("main::SAlias2<T>");
		SAlias2Symbol.TargetType.FullName.Should().Be("main::S<T>");
		SAlias2Symbol.TargetType.Should().BeAssignableTo<NamedTypeSymbol>().Subject.GenericArgs.ElementAt(0).Should()
			.Be(SAlias2Symbol.GenericParams.ElementAt(0));

		var SAlias2ClosedSymbol = (AliasTypeSymbol)reasoner.CloseGeneric(SAlias2Symbol, [project.Workspace.ResolveKnownTypeSymbol(KnownTypeTag.i32)]);
		SAlias2ClosedSymbol.FullName.Should().Be("main::SAlias2<i32>");
		SAlias2ClosedSymbol.TargetType.FullName.Should().Be("main::S<i32>");
		SAlias2ClosedSymbol.TargetType.Should().Be(SAliasSymbol.TargetType);

		var fFnDecl = tree.Root.DescendantNodes().OfType<FnDeclNode>().ElementAt(0);
		var s1VarDecl = fFnDecl.DescendantNodes().OfType<VarDeclNode>().ElementAt(0);
		var s1LocalSymbol = reasoner.GetDeclaredSymbol(s1VarDecl);
		var s2VarDecl = fFnDecl.DescendantNodes().OfType<VarDeclNode>().ElementAt(1);
		var s2LocalSymbol = reasoner.GetDeclaredSymbol(s2VarDecl);
		var s3VarDecl = fFnDecl.DescendantNodes().OfType<VarDeclNode>().ElementAt(2);
		var s3LocalSymbol = reasoner.GetDeclaredSymbol(s3VarDecl);
		var s3LocalSymbolType = s3LocalSymbol.Type.Should().BeAssignableTo<AliasTypeSymbol>().Subject;
		s3LocalSymbolType.FullName.Should().Be("main::SAlias2<i32>");
		s3LocalSymbolType.OriginalDef.FullName.Should().Be("main::SAlias2<T>");

		s1LocalSymbol.Type.UnwrapTypeAliases().Should().Be(s2LocalSymbol.Type.UnwrapTypeAliases());
		s2LocalSymbol.Type.UnwrapTypeAliases().Should().Be(s3LocalSymbol.Type.UnwrapTypeAliases());
	}

	[Fact(Skip = "Not implemented yet")]
	public void Generics_Inference()
	{
		var code = """
			struct S<T1> { a: T1; }
			fn foo<T>(value: T) T { value }
			fn foo() {
				var s = S { a: 42 };
				var a = s.a;

				print_i32(foo<i32>(42));
				print_f64(foo<f64>(4.2));
			}
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

		var fooFnDecl = tree.Root.DescendantNodes().OfType<FnDeclNode>().ElementAt(0);
		var sVarDecl = fooFnDecl.DescendantNodes().OfType<VarDeclNode>().ElementAt(0);
		var sLocalSymbol = reasoner.GetDeclaredSymbol(sVarDecl);
		sLocalSymbol.Type.FullName.Should().Be("main::S<i32>");
		var aVarDecl = fooFnDecl.DescendantNodes().OfType<VarDeclNode>().ElementAt(1);
		var aLocalSymbol = reasoner.GetDeclaredSymbol(aVarDecl);
		aLocalSymbol.Type.KnownTypeTag.Should().Be(KnownTypeTag.i32);
	}

	[Fact]
	public void LookupType_Call()
	{
		var code = """
			fn f1() i32 { return 2; }

			fn f2() {
				f1();
			}
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

		var callExpr = tree.Root.DescendantNodes().OfType<CallExprNode>().Single();
		var callType = reasoner.LookupType(callExpr);
		callType.KnownTypeTag.Should().Be(KnownTypeTag.i32);
	}

	[Fact]
	public void LookupType_Call_FnPtr()
	{
		var code = """
			fn f1() i32 { return 2; }

			fn f2() {
				var f = f1;
				f();
			}
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

		var callExpr = tree.Root.DescendantNodes().OfType<CallExprNode>().Single();
		var callType = reasoner.LookupType(callExpr);
		callType.KnownTypeTag.Should().Be(KnownTypeTag.i32);
	}

	[Fact]
	public void LookupType_Field()
	{
		var code = """
			struct S1 { a: i32 = 42; }
			fn f() { var s = S1.{ }; var a = s.a; }
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

		var varDecl = tree.Root.DescendantNodes().OfType<VarDeclNode>().ElementAt(1);
		var accessExpr = varDecl.Initializer!.Expr;
		var fieldType = reasoner.LookupType(accessExpr);
		fieldType.Should().NotBeNull();
		fieldType.KnownTypeTag.Should().Be(KnownTypeTag.i32);
	}

	[Fact]
	public void LookupType_NumericExplicitType()
	{
		var code = """
			fn f() { var n = 32f32; }
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

		var varDecl = tree.Root.DescendantNodes().OfType<VarDeclNode>().Single();
		var literalExpr = varDecl.Initializer!.Expr;
		var numericType = reasoner.LookupType(literalExpr);
		numericType.KnownTypeTag.Should().Be(KnownTypeTag.f32);
	}

	[Fact]
	public void LookupType_PtrType()
	{
		var code = """
			fn f() {
				var a: i32 = 2;
				var b = &a;
				var c = *b;
			}
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

		var aType = reasoner.GetDeclaredSymbol(tree.Root.DescendantNodes().OfType<VarDeclNode>().ElementAt(0))!.Type;
		var bType = reasoner.GetDeclaredSymbol(tree.Root.DescendantNodes().OfType<VarDeclNode>().ElementAt(1))!.Type;
		var cType = reasoner.GetDeclaredSymbol(tree.Root.DescendantNodes().OfType<VarDeclNode>().ElementAt(2))!.Type;

		aType.KnownTypeTag.Should().Be(KnownTypeTag.i32);
		bType.Should().BeAssignableTo<PtrTypeSymbol>().Subject.InnerType!.KnownTypeTag.Should().Be(KnownTypeTag.i32);
		cType.KnownTypeTag.Should().Be(KnownTypeTag.i32);
	}

	[Fact]
	public void LookupType_PtrType_AutoDeref()
	{
		var code = """
			struct S {
				get() i32 { 42 }
			}

			fn f() {
				var s = S.{ };
				var a = s.get();
				var s_ptr = &s;
				// Pass through type analysis for pointers. This should properly resolve the method.
				var b = s_ptr.get();
			}
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

		var aType = reasoner.GetDeclaredSymbol(tree.Root.DescendantNodes().OfType<VarDeclNode>().ElementAt(1))!.Type;
		var bType = reasoner.GetDeclaredSymbol(tree.Root.DescendantNodes().OfType<VarDeclNode>().ElementAt(3))!.Type;

		aType.KnownTypeTag.Should().Be(KnownTypeTag.i32);
		bType.KnownTypeTag.Should().Be(KnownTypeTag.i32);
	}

	[Fact]
	public void Symbol_UnknownType()
	{
		var code = """
			fn f() {
				var a: Unknown = 2;
			}
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

		var varDecl = tree.Root.DescendantNodes().OfType<VarDeclNode>().Single();
		reasoner.TryGetDeclaredSymbol(varDecl, out var symbol).Should().BeTrue();
		var localSymbol = (LocalSymbol)symbol!;
		localSymbol.Type.Should().BeAssignableTo<UnknownTypeSymbol>();
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
		var code = $$"""
			fn f() {
				var a = {{expr}};
			}
			""";
		ExpectLocalInferredType(code, expectedTypeTag);
	}

	private void ExpectLocalInferredType(string code, KnownTypeTag expectedTypeTag)
	{
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

		var varDecl = tree.Root.DescendantNodes().OfType<VarDeclNode>().Single();
		reasoner.TryGetDeclaredSymbol(varDecl, out var symbol).Should().BeTrue();
		var localSymbol = symbol.Should().BeAssignableTo<LocalSymbol>().Subject;
		localSymbol.Type.KnownTypeTag.Should().Be(expectedTypeTag);
		localSymbol.IsTypeInferred.Should().BeTrue();
	}

	[Fact]
	public void Symbol_InferType_Field()
	{
		var code = """
			struct S1 {
				a = true;
			}
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

		var fieldDecl = tree.Root.DescendantNodes().OfType<FieldDeclNode>().Single();
		reasoner.TryGetDeclaredSymbol(fieldDecl, out var symbol).Should().BeTrue();
		var fieldSymbol = (FieldSymbol)symbol!;
		fieldSymbol.Type.KnownTypeTag.Should().Be(KnownTypeTag.@bool);
		fieldSymbol.IsTypeInferred.Should().BeTrue();
	}

	[Fact]
	public void FnSymbol_Children()
	{
		var code = """
			fn f() {
				fn bar() {}

				{
					var a = 2;
					struct baz {
						fn foo() { }
					}
				}
			}
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

		var fnDecl = tree.Root.DescendantNodes().OfType<FnDeclNode>().First();
		var fnSymbol = reasoner.GetDeclaredSymbol(fnDecl);
		fnSymbol.Children.Should().HaveCount(3);
	}

	[Fact]
	public void FnSymbol_ReturnTypeAsVoidIfNotSet()
	{
		var code = """
			fn f() { }
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

		var fnDecl = tree.Root.DescendantNodes().OfType<FnDeclNode>().Single();
		var fnSymbol = reasoner.GetDeclaredSymbol(fnDecl);
		fnSymbol.ReturnType.KnownTypeTag.Should().Be(KnownTypeTag.@void);
	}

	[Fact]
	public void PtrTypes()
	{
		var code = """
			alias i32 as Integer;

			fn f() {
				var a1: *i32;
				var a2: *i32;
				var a3: *Integer;
				var a4: *;
				var a5: *;
			}
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

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
			var a1Node = tree.Root.DescendantNodes().OfType<VarDeclNode>().ElementAt(index);
			return reasoner.GetDeclaredSymbol(a1Node)!.Type;
		}
	}

	[Fact]
	public void FnTypesForCompatibleFnSigsAreEqual()
	{
		var code = """
			fn f1(a: i32, b: i32) i64 { return 1.0; }
			fn f2(a: i32, b: i32) i64 { return 2.0; }
			fn f3(a: i32, b: i32) { }
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

		var f1Node = tree.Root.DescendantNodes().OfType<FnDeclNode>().ElementAt(0);
		var f1Symbol = reasoner.GetDeclaredSymbol(f1Node)!;
		var f2Node = tree.Root.DescendantNodes().OfType<FnDeclNode>().ElementAt(1);
		var f2Symbol = reasoner.GetDeclaredSymbol(f2Node)!;
		var f3Node = tree.Root.DescendantNodes().OfType<FnDeclNode>().ElementAt(2);
		var f3Symbol = reasoner.GetDeclaredSymbol(f3Node)!;

		f1Symbol.Type.Equals(f2Symbol.Type).Should().BeTrue();
		f1Symbol.Type.Equals(f3Symbol.Type).Should().BeFalse();
	}

	[Fact]
	public void FnTypesWithDifferringExternAreNotEqual()
	{
		var code = """
			@extern fn f1(a: i32, b: i32) i64 { return 1.0; }
			@extern fn f2(a: i32, b: i32) i64 { return 2.0; }
			fn f3(a: i32, b: i32) i64 { return 3.0; }
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

		var f1Node = tree.Root.DescendantNodes().OfType<FnDeclNode>().ElementAt(0);
		var f1Symbol = reasoner.GetDeclaredSymbol(f1Node)!;
		var f2Node = tree.Root.DescendantNodes().OfType<FnDeclNode>().ElementAt(1);
		var f2Symbol = reasoner.GetDeclaredSymbol(f2Node)!;
		var f3Node = tree.Root.DescendantNodes().OfType<FnDeclNode>().ElementAt(2);
		var f3Symbol = reasoner.GetDeclaredSymbol(f3Node)!;

		f1Symbol.Type.Equals(f2Symbol.Type).Should().BeTrue();
		f1Symbol.Type.Equals(f3Symbol.Type).Should().BeFalse();
	}

	[Fact]
	public void FnTypesForCompatibleFnTypesAreEqual()
	{
		var code = """
			fn f() {
				var f1: fn (i32, i32) i64;
				var f2: fn (i32, i32) i64;
				var f3: fn (i32, i32);
			}
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

		var f1Node = tree.Root.DescendantNodes().OfType<VarDeclNode>().ElementAt(0);
		var f1Symbol = reasoner.GetDeclaredSymbol(f1Node)!;
		var f2Node = tree.Root.DescendantNodes().OfType<VarDeclNode>().ElementAt(1);
		var f2Symbol = reasoner.GetDeclaredSymbol(f2Node)!;
		var f3Node = tree.Root.DescendantNodes().OfType<VarDeclNode>().ElementAt(2);
		var f3Symbol = reasoner.GetDeclaredSymbol(f3Node)!;

		f1Symbol.Type.Should().BeAssignableTo<FnTypeSymbol>();
		f1Symbol.Type.Equals(f2Symbol.Type).Should().BeTrue();
		f1Symbol.Type.Equals(f3Symbol.Type).Should().BeFalse();
	}

	[Fact]
	public void FnTypesUsingTypeAliases()
	{
		var code = """
			alias i32 as Integer1;
			alias i32 as Integer2;
			alias f64 as Float;

			fn f() {
				var f1: fn (Integer1, i32, Integer1) f64;
				var f2: fn (i32, Integer2, Integer2) Float;
			}
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

		var f1Node = tree.Root.DescendantNodes().OfType<VarDeclNode>().ElementAt(0);
		var f1Symbol = reasoner.GetDeclaredSymbol(f1Node)!;
		var f2Node = tree.Root.DescendantNodes().OfType<VarDeclNode>().ElementAt(1);
		var f2Symbol = reasoner.GetDeclaredSymbol(f2Node)!;

		f1Symbol.Type.IsAssignableFrom(f2Symbol.Type).Should().BeTrue();
		f2Symbol.Type.IsAssignableFrom(f1Symbol.Type).Should().BeTrue();
	}

	[Fact]
	public void FnTypes_FnTypesWithErrorsAreNotCompatible()
	{
		var code = """
			fn f1(a: Unknown) { }
			fn f2(a: Unknown) { }
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

		var fnDeclNode1 = tree.Root.DescendantNodes().OfType<FnDeclNode>().ElementAt(0);
		var fnSymbol1 = reasoner.GetDeclaredSymbol(fnDeclNode1)!;
		var fnDeclNode2 = tree.Root.DescendantNodes().OfType<FnDeclNode>().ElementAt(1);
		var fnSymbol2 = reasoner.GetDeclaredSymbol(fnDeclNode2)!;

		fnSymbol1.Type.IsAssignableFrom(fnSymbol2.Type).Should().BeFalse();
	}

	[Fact]
	public void Attrs()
	{
		var result = AnalysisTestUtils.CreateFromNodeAnnotatedCode("""
			@public @intrinsic("foo")
			fn some() {}
			""");
		var reasoner = result.Reasoner;

		var attrsListNode = result.Tree.Root.DescendantNodes().OfType<AttrsListNode>().First();
		var attrsList = reasoner.GetAttrs(attrsListNode);

		attrsList.HasPublic.Should().BeTrue();
		attrsList.HasExtern.Should().BeFalse();
		attrsList.TryGetIntrinsic(out var intrinsicAttr).Should().BeTrue();
		intrinsicAttr!.Name.Should().Be("foo");
	}

	[Fact]
	public void FnSymbol_IsExtern()
	{
		var code = """
			@extern fn f();
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

		var fnDecl = tree.Root.DescendantNodes().OfType<FnDeclNode>().Single();
		reasoner.TryGetDeclaredSymbol(fnDecl, out var symbol).Should().BeTrue();
		var fnSymbol = symbol.Should().BeAssignableTo<FnSymbol>().Subject;
		fnSymbol.IsExtern.Should().BeTrue();
	}

	[Fact]
	public void FnSymbol_OuterFn()
	{
		var code = """
			struct S1 { }
			fn f() { }
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

		var fnDecl = tree.Root.DescendantNodes().OfType<FnDeclNode>().Single();
		reasoner.TryGetDeclaredSymbol(fnDecl, out var symbol).Should().BeTrue();
		var fnSymbol = symbol.Should().BeAssignableTo<FnSymbol>().Subject;
		fnSymbol.IsOuterFn.Should().BeTrue();
	}

	[Fact]
	public void FnSymbol_TypeWithoutReceiver()
	{
		var code = """
			struct S1 { a() { } }
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

		var methodDecl = tree.Root.DescendantNodes().OfType<MethodDeclNode>().Single();
		reasoner.TryGetDeclaredSymbol(methodDecl, out var symbol).Should().BeTrue();
		var fnSymbol = symbol.Should().BeAssignableTo<FnSymbol>().Subject;
		fnSymbol.Type.ParamTypes.Should().HaveCount(1); // reciever
		fnSymbol.TypeWithoutReceiver.ParamTypes.Should().HaveCount(0); // no receiver
	}

	// See Reasoner.Builder.CreateSymbol for a note on this.
	[Fact]
	public void EnsureLocalDeclDoesNotIncludeItselfInChildren()
	{
		var code = """
			fn main() {
				struct S {
					a: i32;
				}

				fn f { }
			}
			""";
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(code);

		var sDecl = tree.Root.DescendantNodes().OfType<StructDeclNode>().Single();
		var sSymbol = reasoner.GetDeclaredSymbol(sDecl);
		sSymbol.Members.Should().HaveCount(1);

		var fDecl = tree.Root.DescendantNodes().OfType<FnDeclNode>().Single(x => x.Sig.Name.Identifier.Value == "f");
		var fSymbol = reasoner.GetDeclaredSymbol(fDecl);
		fSymbol.Params.Should().HaveCount(0);
	}
}
