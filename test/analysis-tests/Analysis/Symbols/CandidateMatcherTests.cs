namespace Surab.Analysis;

public class CandidateMatcherTests
{
	[Fact]
	public void FnsMatch()
	{
		var result = AnalysisTestUtils.CreateFromNodeAnnotatedCode("""
			trait Trait1 {
				{|Trait1_Method1:|}method1();
				{|Trait1_Method2:|}method2(v: i32) i32;
			}
			trait Trait2<T1> {
				{|Trait2_Method1:|}method1() T1;
			}
			
			struct Struct1 {
				{|Struct1_ImplTrait1:|}impl Trait1 {
					{|ImplTrait1_Method1:|}method1() {}
					{|ImplTrait1_Method2:|}method2(x: i32) i32 {}
				}

				{|Struct1_ImplTrait2:|}impl Trait2<i32> {
					{|ImplTrait2_Method1:|}method1() i32 {}
				}
			}

			struct Struct2 {
				{|Struct2_ImplTrait1:|}impl Trait1 {
					{|ImplTrait1_Method1_unmatch:|}method1234567() {}
					{|ImplTrait1_Method2_unmatch:|}method1(v: i64) i32 {}
				}
			}
			""");

		var symbol_Trait1_Method1 = result.GetDeclaredMethodSymbol("Trait1_Method1");
		var symbol_Trait1_Method2 = result.GetDeclaredMethodSymbol("Trait1_Method2");
		var symbol_Trait2_Method1 = result.GetDeclaredMethodSymbol("Trait2_Method1");

		var symbol_Struct1_ImplTrait1 = result.GetDeclaredImplSymbol("Struct1_ImplTrait1");
		var symbol_Struct1_ImplTrait2 = result.GetDeclaredImplSymbol("Struct1_ImplTrait2");
		var symbol_Struct2_ImplTrait1 = result.GetDeclaredImplSymbol("Struct2_ImplTrait1");

		var symbol_ImplTrait1_Method1 = result.GetDeclaredMethodSymbol("ImplTrait1_Method1");
		var symbol_ImplTrait1_Method2 = result.GetDeclaredMethodSymbol("ImplTrait1_Method2");
		var symbol_ImplTrait2_Method1 = result.GetDeclaredMethodSymbol("ImplTrait2_Method1");

		var symbol_ImplTrait1_Method1_unmatch = result.GetDeclaredMethodSymbol("ImplTrait1_Method1_unmatch");
		var symbol_ImplTrait1_Method2_unmatch = result.GetDeclaredMethodSymbol("ImplTrait1_Method2_unmatch");

		CandidateMatcher.FnsMatch(symbol_Trait1_Method1, symbol_ImplTrait1_Method1).Should().BeTrue();
		CandidateMatcher.FnsMatch(symbol_Trait1_Method2, symbol_ImplTrait1_Method2).Should().BeTrue();
		CandidateMatcher.FnsMatch((FnSymbol)symbol_Struct1_ImplTrait2.GetMember("method1")!, symbol_ImplTrait2_Method1).Should().BeTrue();

		CandidateMatcher.FnsMatch(symbol_Trait1_Method1, symbol_ImplTrait1_Method1_unmatch).Should().BeFalse();
		CandidateMatcher.FnsMatch(symbol_Trait1_Method2, symbol_ImplTrait1_Method2_unmatch).Should().BeFalse();
	}
}
