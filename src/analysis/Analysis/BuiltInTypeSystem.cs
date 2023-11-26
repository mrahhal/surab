namespace Surab.Analysis;

// TODO: Shouldn't have a static type system here. We should have some kind of static type tag we can match on the symbol somehow.

/// <summary>
/// Contains built in type system symbols.
/// </summary>
public static class BuiltinTypeSystem
{
    static BuiltinTypeSystem()
    {
        Prelude.EnsurePreludeLoaded();
    }

    public static StructTypeSymbol None { get; private set; } = null!;

    public static StructTypeSymbol Number { get; private set; } = null!;

    public static StructTypeSymbol Char { get; private set; } = null!;

    public static StructTypeSymbol Bool { get; private set; } = null!;

    public static void TryRegisterBuiltinSymbol(Symbol symbol)
    {
        if (symbol is StructTypeSymbol structTypeSymbol)
        {
            switch (structTypeSymbol.Name)
            {
                case "None":
                    None = structTypeSymbol;
                    break;

                case "Number":
                    Number = structTypeSymbol;
                    break;

                case "Char":
                    Char = structTypeSymbol;
                    break;

                case "Bool":
                    Bool = structTypeSymbol;
                    break;

                default:
                    break;
            }
        }
    }

    public static bool IsBuiltin(TypeSymbol type)
    {
        // TODO: Do this better.
        return type == None || type == Number || type == Char || type == Bool;
    }
}
