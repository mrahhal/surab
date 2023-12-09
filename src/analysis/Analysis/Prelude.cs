using System.Diagnostics;

namespace Surab.Analysis;

internal static class Prelude
{
    private static Reasoner? _reasoner = null;

    // Should only be called from Reasoner's static ctor so that it's created only once.
    public static void Load()
    {
        _reasoner = CreateReasoner();
    }

    public static Reasoner GetReasoner()
    {
        Debug.Assert(_reasoner != null);
        return _reasoner;
    }

    private static Reasoner CreateReasoner()
    {
        var preludeFile = Path.GetFullPath(Path.Combine(PathHelper.GetSrcFolderPath(), "lib/prelude.surab"));
        var prelude = File.ReadAllText(preludeFile);
        var tree = Parser.ParseModule(prelude);

        var reasoner = Reasoner.Create(tree, isPrelude: true);
        var diags = reasoner.GetAllDiagnostics();

        if (diags.Any(x => x.Level >= DiagnosticLevel.Error))
        {
            throw new Exception("Errors in prelude.");
        }

        return reasoner;
    }
}
