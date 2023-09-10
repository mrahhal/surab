using LLVMSharp.Interop;
using LLVMInterop = LLVMSharp.Interop.LLVM;

namespace Surab.Compilation.LLVM;

/// <summary>
/// Temporary extensions that cover implementations missing in LLVM's api.
/// </summary>
internal static class LLVMExtensions
{
    public static unsafe LLVMAttributeRef CreateEnumAttribute(this LLVMContextRef @this, AttributeKind kind, ulong val = default)
    {
        return LLVMInterop.CreateEnumAttribute(@this, (uint)kind, val);
    }

    public static unsafe void AddAttributeAtIndex(this LLVMValueRef @this, LLVMAttributeIndex idx, LLVMAttributeRef a)
    {
        LLVMInterop.AddAttributeAtIndex(@this, idx, a);
    }

    public static unsafe void AddCallSiteAttributeAtIndex(this LLVMValueRef @this, LLVMAttributeIndex idx, LLVMAttributeRef a)
    {
        LLVMInterop.AddCallSiteAttribute(@this, idx, a);
    }

    public static unsafe CallConv GetFunctionCallConv(this LLVMValueRef @this)
    {
        return (CallConv)@this.FunctionCallConv;
    }

    public static unsafe void SetFunctionCallConv(this LLVMValueRef @this, CallConv cc)
    {
        @this.FunctionCallConv = (uint)cc;
    }
}

internal static class AttributeIndex
{
    public static readonly LLVMAttributeIndex Function = (LLVMAttributeIndex)~0;
    public static readonly LLVMAttributeIndex Return = 0;
    public static readonly LLVMAttributeIndex FirstArg = (LLVMAttributeIndex)1;
}

internal enum AttributeKind
{
#pragma warning disable CA1069 // Enums values should not be duplicated
    None,

    FirstEnumAttr = 1,
    AllocAlign = 1,
    AllocatedPointer = 2,
    AlwaysInline = 3,
    Builtin = 4,
    Cold = 5,
    Convergent = 6,
    DisableSanitizerInstrumentation = 7,
    FnRetThunkExtern = 8,
    Hot = 9,
    ImmArg = 10,
    InReg = 11,
    InlineHint = 12,
    JumpTable = 13,
    MinSize = 14,
    MustProgress = 15,
    Naked = 16,
    Nest = 17,
    NoAlias = 18,
    NoBuiltin = 19,
    NoCallback = 20,
    NoCapture = 21,
    NoCfCheck = 22,
    NoDuplicate = 23,
    NoFree = 24,
    NoImplicitFloat = 25,
    NoInline = 26,
    NoMerge = 27,
    NoProfile = 28,
    NoRecurse = 29,
    NoRedZone = 30,
    NoReturn = 31,
    NoSanitizeBounds = 32,
    NoSanitizeCoverage = 33,
    NoSync = 34,
    NoUndef = 35,
    NoUnwind = 36,
    NonLazyBind = 37,
    NonNull = 38,
    NullPointerIsValid = 39,
    OptForFuzzing = 40,
    OptimizeForSize = 41,
    OptimizeNone = 42,
    PresplitCoroutine = 43,
    ReadNone = 44,
    ReadOnly = 45,
    Returned = 46,
    ReturnsTwice = 47,
    SExt = 48,
    SafeStack = 49,
    SanitizeAddress = 50,
    SanitizeHWAddress = 51,
    SanitizeMemTag = 52,
    SanitizeMemory = 53,
    SanitizeThread = 54,
    ShadowCallStack = 55,
    SkipProfile = 56,
    Speculatable = 57,
    SpeculativeLoadHardening = 58,
    StackProtect = 59,
    StackProtectReq = 60,
    StackProtectStrong = 61,
    StrictFP = 62,
    SwiftAsync = 63,
    SwiftError = 64,
    SwiftSelf = 65,
    WillReturn = 66,
    WriteOnly = 67,
    ZExt = 68,
    LastEnumAttr = 68,
    FirstTypeAttr = 69,
    ByRef = 69,
    ByVal = 70,
    ElementType = 71,
    InAlloca = 72,
    Preallocated = 73,
    StructRet = 74,
    LastTypeAttr = 74,
    FirstIntAttr = 75,
    Alignment = 75,
    AllocKind = 76,
    AllocSize = 77,
    Dereferenceable = 78,
    DereferenceableOrNull = 79,
    Memory = 80,
    StackAlignment = 81,
    UWTable = 82,
    VScaleRange = 83,
    LastIntAttr = 83,

    EndAttrKinds,
    EmptyKey,
    TombstoneKey,
#pragma warning restore CA1069 // Enums values should not be duplicated
}

internal enum CallConv
{
    C = 0,

    Fast = 8,

    Cold = 9,

    Win64 = 79,
}
