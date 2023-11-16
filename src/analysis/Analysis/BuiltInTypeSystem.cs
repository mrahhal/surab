namespace Surab.Analysis;

/// <summary>
/// Contains the temporarily defined built in type system.
/// </summary>
public static class BuiltInTypeSystem
{
    static BuiltInTypeSystem()
    {
        None = new BuiltInStructTypeSymbol(
            new IdentifierToken(TextSpan.Empty, "None"),
            new List<BuiltInStructTypeSymbol> { });

        Number = new BuiltInStructTypeSymbol(
            new IdentifierToken(TextSpan.Empty, "Number"),
            new List<BuiltInStructTypeSymbol> { });

        Char = new BuiltInStructTypeSymbol(
            new IdentifierToken(TextSpan.Empty, "Char"),
            new List<BuiltInStructTypeSymbol> { });

        //String = new BuiltInClassTypeSymbol(
        //    new IdentifierToken(TextSpan.Empty, "String"),
        //    new List<BuiltInNamedTypeSymbol> { });

        Bool = new BuiltInStructTypeSymbol(
            new IdentifierToken(TextSpan.Empty, "Bool"),
            new List<BuiltInStructTypeSymbol> { });
    }

    public static BuiltInStructTypeSymbol None { get; }

    public static BuiltInStructTypeSymbol Number { get; }

    public static  BuiltInStructTypeSymbol Char { get; }

    //public static BuiltInClassTypeSymbol String { get; }

    public static  BuiltInStructTypeSymbol Bool { get; }
}
