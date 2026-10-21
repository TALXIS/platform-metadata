using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Registers the entity in Solution.xml, then sorts entity attributes and normalizes
/// xsi:nil tags. Its forms stay out of root components, the entity row covers them.
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

        EntityAttributeSorter.SortAll(request.SolutionRootPath);
        NilTagNormalizer.NormalizeSolutionXml(request.SolutionRootPath);
        return new ScaffoldResult();
    }
}
