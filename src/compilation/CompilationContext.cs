using LLVMSharp.Interop;
using Surab.Analysis;
using Surab.Compilation.LLVM;

namespace Surab.Compilation;

public sealed class CompilationContext
{
    internal CompilationContext(
        SemanticModel model,
        LoweredTree tree,
        Target target,
        LLVMCompilationContext llvmCompilationContext,
        CompilationOptions options)
    {
        LoweredTree = tree;
        Model = model;
        Target = target;
        LLVM = llvmCompilationContext;
        Options = options;
    }

    internal LoweredTree LoweredTree { get; }

    public SemanticModel Model { get; }

    public Target Target { get; }

    // TODO: Move to FnCompiler? (with the GetOrCreate stuff)
    private Dictionary<Symbol, LLVMUnit> SymbolToValue { get; } = new();

    internal LLVMCompilationContext LLVM { get; }

    public CompilationOptions Options { get; }

    internal LLVMUnit Create(ParamSymbol paramSymbol, LLVMValueRef value)
    {
        var typeRef = GetForType(paramSymbol.Type);

        var ptr = LLVM.Builder.BuildAlloca(typeRef, paramSymbol.Identifier?.Value ?? string.Empty);
        var unit = new LLVMUnit(typeRef, ptr);
        LLVM.Builder.BuildStore(value, ptr);

        SymbolToValue[paramSymbol] = unit;
        return unit;
    }

    internal LLVMUnit Create(LocalSymbol localSymbol)
    {
        var typeRef = GetForType(localSymbol.Type);

        var ptr = LLVM.Builder.BuildAlloca(typeRef, localSymbol.Identifier?.Value ?? string.Empty);
        var unit = new LLVMUnit(typeRef, ptr);

        SymbolToValue[localSymbol] = unit;
        return unit;
    }

    internal LLVMUnit GetOrCreate(Symbol symbol)
    {
        if (SymbolToValue.TryGetValue(symbol, out var unit))
        {
            return unit;
        }

        var created = symbol switch
        {
            //ConstSymbol constSymbol => Create(constSymbol),
            FnSymbol fnSymbol => Create(fnSymbol),

            _ => throw new NotImplementedException(),
        };
        SymbolToValue[symbol] = created;

        return created;
    }

    //private LLVMUnit Create(ConstSymbol constSymbol)
    //{
    //    // TODO:
    //    var type = constSymbol.Type!;
    //    if (type == BuiltInTypeSystem.Number)
    //    {
    //        throw new Exception();
    //        //LLVMValueRef.CreateConstReal(LLVMTypeRef.Double, ???);
    //    }
    //    else
    //    {
    //        throw new Exception();
    //    }
    //}

    private LLVMUnit Create(FnSymbol fnSymbol)
    {
        var returnType = GetForType(fnSymbol.ReturnType);
        var @params = fnSymbol.Params.Select(p => GetForType(p.Type)).ToArray();

        var fnType = LLVMTypeRef.CreateFunction(returnType, @params, false);
        var fnValue = LLVM.Module.AddFunction(fnSymbol.Identifier!.Value, fnType);

        return new LLVMUnit(fnType, fnValue);
    }

    private static LLVMTypeRef GetForType(TypeSymbol type)
    {
        if (type == BuiltInTypeSystem.None)
        {
            return LLVMTypeRef.Void;
        }
        else if (type == BuiltInTypeSystem.Number)
        {
            return LLVMTypeRef.Double;
        }

        throw new Exception();
    }
}
