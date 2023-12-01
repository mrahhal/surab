using LLVMSharp.Interop;
using Surab.Analysis;

namespace Surab.Compilation;

public partial class TargetTests
{
    [Fact]
    public void LowerType_Struct_1()
    {
        var target = CreateTarget();

        var structLayout = target.GetStructLayout(TestStructs.Struct1);
        var llvmType = target.LowerType(TestStructs.Struct1);

        llvmType.StructElementTypes.Should().HaveCount(structLayout.Sections.Count);
        llvmType.StructElementTypes[0].Should().Be(LLVMTypeRef.Int1);
        llvmType.StructElementTypes[1].Should().Be(LLVMTypeRef.CreateArray(LLVMTypeRef.Int1, 7)); // Padding
        llvmType.StructElementTypes[2].Should().Be(LLVMTypeRef.Double);
    }

    [Fact]
    public void LowerType_Struct_2()
    {
        var target = CreateTarget();

        var structLayout = target.GetStructLayout(TestStructs.Struct2);
        var llvmType = target.LowerType(TestStructs.Struct2);

        llvmType.StructElementTypes.Should().HaveCount(structLayout.Sections.Count);
        llvmType.StructElementTypes[0].Should().Be(LLVMTypeRef.Int1);
        llvmType.StructElementTypes[1].Should().Be(LLVMTypeRef.CreateArray(LLVMTypeRef.Int1, 7)); // Padding
        llvmType.StructElementTypes[2].Should().Be(LLVMTypeRef.Double);
        llvmType.StructElementTypes[3].Should().Be(LLVMTypeRef.Int1);
        llvmType.StructElementTypes[4].Should().Be(LLVMTypeRef.CreateArray(LLVMTypeRef.Int1, 7)); // Padding
    }

    private static Target CreateTarget()
    {
        SemanticModel.EnsurePreludeLoaded();
        return new x86_64WindowsMSVCTarget();
    }
}
