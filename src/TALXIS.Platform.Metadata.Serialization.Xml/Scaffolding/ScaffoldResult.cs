namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

public sealed class ScaffoldResult
{
    private readonly List<string> _warnings = new();
    public IReadOnlyList<string> Warnings => _warnings;
    internal void AddWarning(string message) => _warnings.Add(message);
}
