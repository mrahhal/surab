namespace Surab.Compilation.LLVM;

internal class SCPPFnAttrsDef
{
	private readonly Dictionary<int, List<SCPPAttributeRef>> _map = [];

	public void Add(int index, params SCPPAttributeRef[] attrs)
	{
		if (!_map.TryGetValue(index, out var list))
		{
			list = [];
			_map[index] = list;
		}

		list.AddRange(attrs);
	}

	public void AddFunction(params SCPPAttributeRef[] attrs) => Add(SCPPAttributeIndex.Function, attrs);

	public void AddReturn(params SCPPAttributeRef[] attrs) => Add(SCPPAttributeIndex.Return, attrs);

	public void AddParam(int paramOffset, params SCPPAttributeRef[] attrs) => Add(paramOffset + 1, attrs);

	public void Apply(Action<int, SCPPAttributeRef> action)
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
