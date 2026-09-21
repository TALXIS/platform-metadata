using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Registers the entity and its rendered forms in Solution.xml, then sorts
/// entity attributes and normalizes xsi:nil tags.
/// </summary>
public static class EntityScaffold
{
    public static ScaffoldResult Apply(EntityScaffoldRequest request)
    {
        SolutionRootComponentPatcher.EnsureRootComponent(request.SolutionRootPath, new RootComponent
        {
            Type = ComponentType.Entity,
            SchemaName = request.EntitySchemaName,
            Behavior = request.Behavior,
        });

        // Registers forms in this order: quick create, main, card, quick view.
        AddFormComponent(request.SolutionRootPath, request.QuickCreateFormId);
        AddFormComponent(request.SolutionRootPath, request.MainFormId);
        AddFormComponent(request.SolutionRootPath, request.CardFormId);
        AddFormComponent(request.SolutionRootPath, request.QuickFormId);

        EntityAttributeSorter.SortAll(request.SolutionRootPath);
        NilTagNormalizer.NormalizeSolutionXml(request.SolutionRootPath);
        return new ScaffoldResult();
    }

    private static void AddFormComponent(string solutionRootPath, string? formId)
    {
        if (formId is null) return;
        SolutionRootComponentPatcher.EnsureRootComponent(solutionRootPath, new RootComponent
        {
            Type = ComponentType.SystemForm,
            Id = Guid.Parse(formId),
            Behavior = 0,
        });
    }
}
