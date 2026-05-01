namespace TALXIS.Platform.Metadata.Solutions;

public sealed class RootComponent
{
    public required int TypeCode { get; set; }
    public string? SchemaName { get; set; }
    public Guid? Id { get; set; }
    public int Behavior { get; set; }
}
