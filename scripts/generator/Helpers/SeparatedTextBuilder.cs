public class SeparatedTextBuilder(string separator = ", ")
{
	private List<string> _list = [];

	public string Separator { get; } = separator;

	public SeparatedTextBuilder Add(string text)
	{
		_list.Add(text);
		return this;
	}

	public string Build()
	{
		var built = string.Join(Separator, _list);
		_list = [];
		return built;
	}
}
