namespace Surab.Analysis;

/// <summary>
/// Represents a resolved type.
/// </summary>
public class ResolvedType
{
    private ResolvedType(TypeSymbol type, bool isInferred)
    {
        Type = type;
        IsInferred = isInferred;
    }

    /// <summary>
    /// The resolved type.
    /// </summary>
    public TypeSymbol Type { get; }

    /// <summary>
    /// Whether the resolved type was inferred or not.
    /// </summary>
    public bool IsInferred { get; }

    public static ResolvedType Typed(TypeSymbol type) => new(type, false);

    public static ResolvedType Inferred(TypeSymbol type) => new(type, true);

    public static implicit operator TypeSymbol(ResolvedType resolvedType) => resolvedType.Type;
}
