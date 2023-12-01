using System.Collections.Immutable;
using Surab.Analysis;

namespace Surab.Compilation;

public static class TestStructs
{
    /**
     * Some structs to be used in tests.
     */

    // 0|1|2|3|4|5|6|7|8|9|10|11|12|13|14|15|
    // a|      -      |b                    |
    public static readonly StructTypeSymbol Struct1 = CreateStruct(
        [
            new("a", BuiltinTypeSystem.@char),
            new("b", BuiltinTypeSystem.f64),
        ]);

    // 0|1|2|3|4|5|6|7|8|9|10|11|12|13|14|15|16|17|18|19|20|21|22|23|
    // a|      -      |b                    |c |         -          |
    public static readonly StructTypeSymbol Struct2 = CreateStruct(
        [
            new("a", BuiltinTypeSystem.@char),
            new("b", BuiltinTypeSystem.f64),
            new("c", BuiltinTypeSystem.@char),
        ]);

    public static StructTypeSymbol CreateStruct(IEnumerable<FieldSymbolTestsImpl> fields)
    {
        return new StructTypeSymbolTestsImpl(
            new IdentifierToken(TextSpan.Empty, "TestStruct"),
            fields);
    }
}

public sealed class StructTypeSymbolTestsImpl : StructTypeSymbol
{
    private readonly IdentifierToken _name;
    private readonly ImmutableArray<Symbol> _members;

    internal StructTypeSymbolTestsImpl(
        IdentifierToken name,
        IEnumerable<Symbol> members) : base(null)
    {
        _name = name;
        _members = members.ToImmutableArray();
    }

    public override IdentifierToken? Identifier => _name;

    public override ImmutableArray<Symbol> Members => _members;
}

public sealed class FieldSymbolTestsImpl(string identifier, TypeSymbol type) : FieldSymbol
{
    private TypeSymbol _enclosingType = null!;

    public override IdentifierToken? Identifier { get; } = new IdentifierToken(TextSpan.Empty, identifier);

    public override TypeSymbol Type { get; } = type;

    public override TypeSymbol EnclosingType => _enclosingType;

    public override bool IsTypeInferred => false;

    public override int FieldIndex => -1;

    public override bool IsPublic => false;

    public void SetEnclosingType(TypeSymbol enclosingType)
    {
        _enclosingType = enclosingType;
    }
}
