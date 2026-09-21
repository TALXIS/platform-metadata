namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Fills the rendered global option set with the requested options and registers
/// it in Solution.xml (type 9, by schema name) through the workspace model.
/// </summary>
public static class OptionSetGlobalScaffold
{
    public static ScaffoldResult Apply(OptionSetGlobalScaffoldRequest request)
    {
        var options = OptionSetOptionsApplier.ParseOptions(request.Options);
        OptionSetOptionsApplier.ApplyToGlobalOptionSet(
            request.SolutionRootPath,
            request.OptionSetName,
            request.OptionSetName,
            options);
        return new ScaffoldResult();
    }
}
