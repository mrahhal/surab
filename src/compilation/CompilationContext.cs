using LLVMSharp.Interop;
using Surab.Analysis;
using Surab.Compilation.LLVM;

namespace Surab.Compilation;

public sealed class CompilationContext
{
    internal CompilationContext(
        LoweredTree tree,
        SemanticModel model,
        LLVMCompilationContext llvmCompilationContext,
        CompilationOptions options)
    {
        LoweredTree = tree;
        Model = model;
        LLVM = llvmCompilationContext;
        Options = options;
    }

    internal LoweredTree LoweredTree { get; }

    public SemanticModel Model { get; }

    // TODO: Move to FunctionCompiler? (with the GetOrCreate stuff)
    private Dictionary<Symbol, LLVMUnit> SymbolToValue { get; } = new();

    internal LLVMCompilationContext LLVM { get; }

    public CompilationOptions Options { get; }

    internal LLVMUnit Create(ParameterSymbol parameterSymbol, LLVMValueRef value)
    {
        var typeRef = GetForType(parameterSymbol.Type);

        var ptr = LLVM.Builder.BuildAlloca(typeRef, parameterSymbol.Identifier?.Value ?? string.Empty);
        var unit = new LLVMUnit(typeRef, ptr);
        LLVM.Builder.BuildStore(value, ptr);

        SymbolToValue[parameterSymbol] = unit;
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
            FunctionSymbol functionSymbol => Create(functionSymbol),

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

    private LLVMUnit Create(FunctionSymbol functionSymbol)
    {
        var returnType = GetForType(functionSymbol.ReturnType);
        var @params = functionSymbol.Parameters.Select(p => GetForType(p.Type)).ToArray();

        var functionType = LLVMTypeRef.CreateFunction(returnType, @params, false);
        var functionValue = LLVM.Module.AddFunction(functionSymbol.Identifier!.Value, functionType);

        return new LLVMUnit(functionType, functionValue);
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
