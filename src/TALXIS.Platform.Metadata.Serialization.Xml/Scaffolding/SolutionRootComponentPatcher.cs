using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Registers a root component in the solution manifest through the typed workspace
/// model (load, mutate, write), replacing the per-template AddXToSolutionXml
/// post-action scripts. First scaffold piece on the metamodel path: no XML patching.
/// </summary>
public static class SolutionRootComponentPatcher
{
    /// <summary>
    /// Adds the root component to the solution manifest unless an equivalent one
    /// (same type and schema name or id) is already registered; returns true when added.
    /// </summary>
    public static bool EnsureRootComponent(string solutionRootPath, RootComponent component)
    {
        var workspace = new XmlWorkspaceReader().Load(solutionRootPath);
        var solution = workspace.Solutions.FirstOrDefault()
            ?? throw new InvalidOperationException($"No solution manifest found in '{solutionRootPath}'.");

        if (solution.RootComponents.Any(rc => Matches(rc, component))) return false;

        solution.AddRootComponent(component);
        new XmlWorkspaceWriter().Write(workspace, solutionRootPath);
        return true;
    }

    private static bool Matches(RootComponent existing, RootComponent candidate)
    {
        if (existing.Type != candidate.Type) return false;
        if (candidate.SchemaName != null)
            return string.Equals(existing.SchemaName, candidate.SchemaName, StringComparison.OrdinalIgnoreCase);
        return candidate.Id != null && existing.Id == candidate.Id;
    }
}
