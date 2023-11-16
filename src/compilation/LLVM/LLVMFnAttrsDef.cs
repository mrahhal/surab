using LLVMSharp.Interop;

namespace Surab.Compilation.LLVM;

internal class LLVMFnAttrsDef
{
    private readonly Dictionary<LLVMAttributeIndex, List<LLVMAttributeRef>> _map = [];

    public void Add(LLVMAttributeIndex index, params LLVMAttributeRef[] attrs)
    {
        if (!_map.TryGetValue(index, out var list))
        {
            list = [];
            _map[index] = list;
        }

        list.AddRange(attrs);
    }

    public void Add(int index, params LLVMAttributeRef[] attrs) => Add((LLVMAttributeIndex)index, attrs);

    public void AddFunction(params LLVMAttributeRef[] attrs) => Add(LLVMAttributeIndex.LLVMAttributeFunctionIndex, attrs);

    public void AddReturn(params LLVMAttributeRef[] attrs) => Add(LLVMAttributeIndex.LLVMAttributeReturnIndex, attrs);

    public void AddParam(int paramOffset, params LLVMAttributeRef[] attrs) => Add((LLVMAttributeIndex)paramOffset + 1, attrs);

    public void Apply(Action<LLVMAttributeIndex, LLVMAttributeRef> action)
    {
        foreach (var item in _map)
        {
            foreach (var attr in item.Value)
            {
                action(item.Key, attr);
            }
        }
    }
}
