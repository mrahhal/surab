using Surab.Compilation.LLVM;

namespace Surab.Compilation;

public partial class TargetTests
{
	[Fact]
	public void LowerType_Struct_1()
	{
		var testContext = CompilationTestContext.Create();
		var context = testContext.Context;

		var structLayout = context.Target.GetStructLayout(testContext.Struct1);
		var llvmType = context.LLVMTarget.LowerType(testContext.Struct1);

		var structElementTypes = llvmType.GetStructElementTypes();
		structElementTypes.Should().HaveCount(structLayout.Sections.Count);
		structElementTypes[0].Should().Be(SCPPTypeRef.Int1);
		structElementTypes[1].Should().Be(SCPPTypeRef.CreateArray(SCPPTypeRef.Int1, 7)); // Padding
		structElementTypes[2].Should().Be(SCPPTypeRef.Double);
	}

	[Fact]
	public void LowerType_Struct_2()
	{
		var testContext = CompilationTestContext.Create();
		var context = testContext.Context;

		var structLayout = context.Target.GetStructLayout(testContext.Struct2);
		var llvmType = context.LLVMTarget.LowerType(testContext.Struct2);

		var structElementTypes = llvmType.GetStructElementTypes();
		structElementTypes.Should().HaveCount(structLayout.Sections.Count);
		structElementTypes[0].Should().Be(SCPPTypeRef.Int1);
		structElementTypes[1].Should().Be(SCPPTypeRef.CreateArray(SCPPTypeRef.Int1, 7)); // Padding
		structElementTypes[2].Should().Be(SCPPTypeRef.Double);
		structElementTypes[3].Should().Be(SCPPTypeRef.Int1);
		structElementTypes[4].Should().Be(SCPPTypeRef.CreateArray(SCPPTypeRef.Int1, 7)); // Padding
	}
}
