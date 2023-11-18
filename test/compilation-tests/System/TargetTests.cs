using Surab.Analysis;

namespace Surab.Compilation;

public partial class TargetTests
{
    private static Target CreateTarget()
    {
        return new x86_64WindowsMSVCTarget();
    }

    private static BuiltInStructTypeSymbol CreateStruct(IEnumerable<FieldSymbolTestsImpl> fields)
    {
        return new BuiltInStructTypeSymbol(
            new IdentifierToken(TextSpan.Empty, "TestStruct"),
            fields);
    }

    private sealed class FieldSymbolTestsImpl(string identifier, TypeSymbol type) : FieldSymbol
    {
        public override IdentifierToken? Identifier { get; } = new IdentifierToken(TextSpan.Empty, identifier);

        public override TypeSymbol Type { get; } = type;
    }
}
