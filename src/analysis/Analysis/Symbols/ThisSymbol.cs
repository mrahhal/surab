using System.Diagnostics;

namespace Surab.Analysis;

[DebuggerDisplay("{ToString()}")]
internal readonly record struct ThisSymbolKey(TypeSymbol type)
{
	public readonly TypeSymbol Type { get; } = type;

	public override string ToString() => "this";
}

public sealed class ThisSymbol : Symbol, ISymbolHasType
{
	internal ThisSymbol(ThisSymbolKey key) : base(SymbolKind.This)
	{
		Key = key;
	}

	public override Symbol OriginalDef => this;

	public override SyntaxNode? DeclaringNode => null;

	internal override IdentifierToken? IdentifierToken => null;

	internal ThisSymbolKey Key { get; }

	public override string Name => "this";

	/// <summary>
	/// The type of `this`. Includes the pointer in the case the receiver is by ref.
	/// </summary>
	public TypeSymbol Type => Key.Type;

	public bool IsTypeInferred => false;
}
