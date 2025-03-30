using System.Diagnostics.CodeAnalysis;

namespace Surab.Analysis;

public class AttrsList
{
	public static readonly IReadOnlyList<AttrDef> Defs =
	[
		new("public", [], (node, _) => new PublicAttr(node)),
		new("extern", [], (node, _) => new ExternAttr(node)),
		new("intrinsic", [AttrDef.ArgType.String], (node, d) => new IntrinsicAttr(node, (string)d[0])),
	];

	public static readonly AttrsList Empty = new([]);

	public AttrsList(IReadOnlyList<Attr> attrs)
	{
		Attrs = attrs;

		HasPublic = Attrs.Any(a => a is PublicAttr);
		HasExtern = Attrs.Any(a => a is ExternAttr);
	}

	public IReadOnlyList<Attr> Attrs { get; }

	public bool HasPublic { get; }

	public bool HasExtern { get; }

	public bool TryGetIntrinsic([NotNullWhen(true)] out IntrinsicAttr? attr)
	{
		attr = Attrs.OfType<IntrinsicAttr>().FirstOrDefault();
		return attr != null;
	}
}

public class AttrDef
{
	public AttrDef(string name, ArgType[] args, Func<AttrNode, List<object>, Attr> create)
	{
		Name = name;
		Args = args;
		Create = create;
	}

	public string Name { get; }

	public IReadOnlyList<ArgType> Args { get; }

	public Func<AttrNode, List<object>, Attr> Create { get; }

	public enum ArgType
	{
		String,
	}
}

public class Attr
{
	public Attr(AttrNode node)
	{
		Node = node;
	}

	public AttrNode Node { get; }
}

public class InvalidAttr : Attr
{
	public InvalidAttr(AttrNode node) : base(node)
	{
	}
}

public class PublicAttr : Attr
{
	public PublicAttr(AttrNode node) : base(node)
	{
	}
}

public class ExternAttr : Attr
{
	public ExternAttr(AttrNode node) : base(node)
	{
	}
}

public class IntrinsicAttr : Attr
{
	public IntrinsicAttr(AttrNode node, string name) : base(node)
	{
		Name = name;
	}

	public string Name { get; }
}
