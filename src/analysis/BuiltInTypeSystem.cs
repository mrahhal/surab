namespace Surab.Analysis;

/// <summary>
/// Contains the temporarily defined built in type system.
/// </summary>
public static class BuiltInTypeSystem
{
    static BuiltInTypeSystem()
    {
        None = new BuiltInNamedTypeSymbol(
            new IdentifierToken(TextSpan.Empty, "None"),
            new List<BuiltInNamedTypeSymbol> { });

        Number = new BuiltInNamedTypeSymbol(
            new IdentifierToken(TextSpan.Empty, "Number"),
            new List<BuiltInNamedTypeSymbol> { });

        Char = new BuiltInNamedTypeSymbol(
            new IdentifierToken(TextSpan.Empty, "Char"),
            new List<BuiltInNamedTypeSymbol> { });

        String = new BuiltInNamedTypeSymbol(
            new IdentifierToken(TextSpan.Empty, "String"),
            new List<BuiltInNamedTypeSymbol> { });

        Bool = new BuiltInNamedTypeSymbol(
            new IdentifierToken(TextSpan.Empty, "Bool"),
            new List<BuiltInNamedTypeSymbol> { });
    }

    public static BuiltInNamedTypeSymbol None { get; }

    public static BuiltInNamedTypeSymbol Number { get; }

    public static  BuiltInNamedTypeSymbol Char { get; }

    public static  BuiltInNamedTypeSymbol String { get; }

    public static  BuiltInNamedTypeSymbol Bool { get; }
}
