using Surab.Analysis;

namespace Surab.Compilation;

public static class TestStructs
{
    /**
     * Some structs to be used in tests.
     */

    // 0|1|2|3|4|5|6|7|8|9|10|11|12|13|14|15|
    // a|      -      |b                    |
    public static readonly BuiltInStructTypeSymbol Struct1 = CreateStruct(
        [
            new("a", BuiltInTypeSystem.Char),
            new("b", BuiltInTypeSystem.Number),
        ]);

    // 0|1|2|3|4|5|6|7|8|9|10|11|12|13|14|15|16|17|18|19|20|21|22|23|
    // a|      -      |b                    |c |         -          |
    public static readonly BuiltInStructTypeSymbol Struct2 = CreateStruct(
        [
            new("a", BuiltInTypeSystem.Char),
            new("b", BuiltInTypeSystem.Number),
            new("c", BuiltInTypeSystem.Char),
        ]);

    public static BuiltInStructTypeSymbol CreateStruct(IEnumerable<FieldSymbolTestsImpl> fields)
    {
        return new BuiltInStructTypeSymbol(
            new IdentifierToken(TextSpan.Empty, "TestStruct"),
            fields);
    }
}

public sealed class FieldSymbolTestsImpl(string identifier, TypeSymbol type) : FieldSymbol
{
    private TypeSymbol _enclosingType = null!;

    public override IdentifierToken? Identifier { get; } = new IdentifierToken(TextSpan.Empty, identifier);

    public override TypeSymbol Type { get; } = type;

    public override TypeSymbol EnclosingType => _enclosingType;

    public override bool IsTypeInferred => false;

    public override int FieldIndex => -1;

    public void SetEnclosingType(TypeSymbol enclosingType)
    {
        _enclosingType = enclosingType;
    }
}
