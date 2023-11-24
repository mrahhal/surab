namespace Surab.Analysis;

internal static class Preclude
{
    // TODO: Find a better way to do this.
    private static SemanticModel? _precludeSemanticModel = null;

    public static void EnsurePrecludeLoaded()
    {
        GetPrecludeSemanticModel();
    }

    public static SemanticModel GetPrecludeSemanticModel()
    {
        return _precludeSemanticModel ??= CreatePrecludeSemanticModel();
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
