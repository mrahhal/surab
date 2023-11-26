namespace Surab.Analysis;

internal static class Prelude
{
    // TODO: Find a better way to do this (so as not to use static?).
    private static SemanticModel? _semanticModel = null;
    private static readonly object _lock = new();

    public static void EnsureLoaded()
    {
        GetSemanticModel();
    }

    public static SemanticModel GetSemanticModel()
    {
        if (_semanticModel != null) return _semanticModel;
        // Need a lock to avoid concurrency issues when running parallel tests.
        lock (_lock)
        {
            // Double lock test.
            if (_semanticModel != null) return _semanticModel;
            return _semanticModel ??= CreateSemanticModel();
        }
    }

    private static SemanticModel CreateSemanticModel()
    {
        var preludeFile = Path.GetFullPath(Path.Combine(PathHelper.GetSrcFolderPath(), "lib/prelude.surab"));
        var prelude = File.ReadAllText(preludeFile);
        var tree = Parser.ParseModule(prelude);

        var model = SemanticModel.Create(tree, isPrelude: true);
        var diags = model.GetAllDiagnostics();

        if (diags.Any(x => x.Level >= DiagnosticLevel.Error))
        {
            throw new Exception("Errors in prelude.");
        }

        return model;
    }
}
