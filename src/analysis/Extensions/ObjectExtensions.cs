using System.Diagnostics.CodeAnalysis;

namespace Surab;

internal static class ObjectExtensions
{
    /// <summary>
    /// Performs a guaranteed cast. Throws on failure.
    /// </summary>
    [return: NotNullIfNotNull(nameof(obj))]
    public static T? Cast<T>(this object? obj)
        where T : class
    {
        if (obj == null) return null;
        return obj as T ?? throw new InvalidCastException($"{obj.GetType().Name} wasn't of type {typeof(T).Name}.");
    }
}
