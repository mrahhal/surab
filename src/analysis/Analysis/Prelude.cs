namespace Surab.Analysis;

internal static class Prelude
{
    // TODO: Find a better way to do this (so as not to use static?).
    private static SemanticModel? _preludeSemanticModel = null;
    private static readonly object _lock = new();

    public static void EnsurePreludeLoaded()
    {
        GetPreludeSemanticModel();
    }

    public static SemanticModel GetPreludeSemanticModel()
    {
        if (_preludeSemanticModel != null) return _preludeSemanticModel;
        // Need a lock to avoid concurrency issues when running parallel tests.
        lock (_lock)
        {
            // Double lock test.
            if (_preludeSemanticModel != null) return _preludeSemanticModel;
            return _preludeSemanticModel ??= CreatePreludeSemanticModel();
        }
    }

    private static SemanticModel CreatePreludeSemanticModel()
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
