namespace Surab.Analysis;

internal static class Preclude
{
    // TODO: Find a better way to do this (so as not to use static?).
    private static SemanticModel? _precludeSemanticModel = null;
    private static readonly object _lock = new();

    public static void EnsurePrecludeLoaded()
    {
        GetPrecludeSemanticModel();
    }

    public static SemanticModel GetPrecludeSemanticModel()
    {
        if (_precludeSemanticModel != null) return _precludeSemanticModel;
        // Need a lock to avoid concurrency issues when running parallel tests.
        lock (_lock)
        {
            // Double lock test.
            if (_precludeSemanticModel != null) return _precludeSemanticModel;
            return _precludeSemanticModel ??= CreatePrecludeSemanticModel();
        }
    }

    private static SemanticModel CreatePrecludeSemanticModel()
    {
        var precludeFile = Path.GetFullPath(Path.Combine(PathHelper.GetSrcFolderPath(), "lib/preclude.surab"));
        var preclude = File.ReadAllText(precludeFile);
        var tree = Parser.ParseModule(preclude);

        var model = SemanticModel.Create(tree, usePreclude: false);
        var diags = model.GetAllDiagnostics();

        if (diags.Any(x => x.Level >= DiagnosticLevel.Error))
        {
            throw new Exception("Errors in preclude.");
        }

        return model;
    }
}
