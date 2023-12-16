using LLVMSharp.Interop;

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

		llvmType.StructElementTypes.Should().HaveCount(structLayout.Sections.Count);
		llvmType.StructElementTypes[0].Should().Be(LLVMTypeRef.Int1);
		llvmType.StructElementTypes[1].Should().Be(LLVMTypeRef.CreateArray(LLVMTypeRef.Int1, 7)); // Padding
		llvmType.StructElementTypes[2].Should().Be(LLVMTypeRef.Double);
	}

	[Fact]
	public void LowerType_Struct_2()
	{
		var testContext = CompilationTestContext.Create();
		var context = testContext.Context;

		var structLayout = context.Target.GetStructLayout(testContext.Struct2);
		var llvmType = context.LLVMTarget.LowerType(testContext.Struct2);

		llvmType.StructElementTypes.Should().HaveCount(structLayout.Sections.Count);
		llvmType.StructElementTypes[0].Should().Be(LLVMTypeRef.Int1);
		llvmType.StructElementTypes[1].Should().Be(LLVMTypeRef.CreateArray(LLVMTypeRef.Int1, 7)); // Padding
		llvmType.StructElementTypes[2].Should().Be(LLVMTypeRef.Double);
		llvmType.StructElementTypes[3].Should().Be(LLVMTypeRef.Int1);
		llvmType.StructElementTypes[4].Should().Be(LLVMTypeRef.CreateArray(LLVMTypeRef.Int1, 7)); // Padding
	}
}
