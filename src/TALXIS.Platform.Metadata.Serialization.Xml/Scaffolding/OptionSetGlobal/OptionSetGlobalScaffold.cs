namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// In-process replacement for the pp-optionset-global template post-action script:
/// fills the rendered global option set with the requested options and registers it
/// in Solution.xml (type 9, by schema name), all through the workspace model.
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
