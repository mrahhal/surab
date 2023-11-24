namespace Surab.Analysis;

// TODO: Shouldn't have a static type system here. We should have some kind of static type tag we can match on the symbol somehow.

/// <summary>
/// Contains the temporarily defined built in type system.
/// </summary>
public static class BuiltInTypeSystem
{
    static BuiltInTypeSystem()
    {
        Preclude.EnsurePrecludeLoaded();
    }

    public static StructTypeSymbol None { get; private set; } = null!;

    public static StructTypeSymbol Number { get; private set; } = null!;

    public static StructTypeSymbol Char { get; private set; } = null!;

    public static StructTypeSymbol Bool { get; private set; } = null!;

    public static void TryRegisterBuiltInType(TypeSymbol type)
    {
        if (type is not StructTypeSymbol structTypeSymbol) return;

        switch (type.Name)
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

    public static bool IsBuiltIn(TypeSymbol type)
    {
        // TODO: Do this better.
        return type == None || type == Number || type == Char || type == Bool;
    }
}
