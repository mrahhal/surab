using System.Diagnostics;

namespace Surab.Analysis;

internal static class Prelude
{
    private static SemanticModel? _semanticModel = null;

    // Should only be called from SemanticModel's static ctor so that it's created only once.
    public static void Load()
    {
        _semanticModel = CreateSemanticModel();
    }

    public static SemanticModel GetSemanticModel()
    {
        Debug.Assert(_semanticModel != null);
        return _semanticModel;
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
