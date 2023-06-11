namespace Surab.Compiler;

internal sealed class IRModule
{
    //public required List<IRGlobal> Globals { get; set; }
    public List<IRFunction> Functions { get; set; } = new();
    //public required List<IRType> Types { get; set; }
}

//internal sealed class IRGlobal
//{
//    public required TypeSymbol Ty { get; set; }
//    public required byte[] Value { get; set; }
//}

internal sealed class IRFunction
{
    public required string Name { get; set; }
    //public required List<IRLocal> Arguments { get; set; }
    public required List<IRLocal> Locals { get; set; }
    public required byte[] Code { get; set; }
}

//internal sealed class IRType
//{
//    public required List<IRField> Fields { get; set; }
//    public required List<IRFunction> Functions { get; set; }
//}

internal sealed class IRLocal
{
    public required string Name { get; set; }
    public required string Ty { get; set; }
}

//internal sealed class IRCode
//{
//    public required List<IROp> Ops { get; set; }
//}

//internal sealed record IROp(
//    OpCode Op,
//    byte[] Data);

//internal sealed class IROp
//{
//    public required OpCode Op { get; set; }

//    public required byte[] Data { get; set; }
//}
