using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class FormXmlLocatorTests : IDisposable
{
    private const string EntityName = "udpp_warehouseitem";
    private const string FormId = "a1b2c3d4-e5f6-4a1b-8c2d-000000000001";

    private readonly string _root = Directory.CreateTempSubdirectory("metadata-form-locator").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string AddForm(string entity, string formType, string formId, DateTime? lastWrite = null)
    {
        var directory = Path.Combine(_root, "Entities", entity, "FormXml", formType);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{{{formId}}}.xml");
        File.WriteAllText(path, "<forms />");
        if (lastWrite != null) File.SetLastWriteTime(path, lastWrite.Value);
        return path;
    }

    [Fact]
    public void Locate_AllKnown_ReturnsExactPath()
    {
        var expected = AddForm(EntityName, "main", FormId);
        Assert.Equal(expected, FormXmlLocator.Locate(_root, EntityName, "main", FormId));
    }

    [Fact]
    public void Locate_AllKnown_MissingFile_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => FormXmlLocator.Locate(_root, EntityName, "main", FormId));
    }

    [Fact]
    public void Locate_OnlyFormIdKnown_FindsFileByName()
    {
        AddForm("udpp_other", "main", "00000000-0000-0000-0000-0000000000aa");
        var expected = AddForm(EntityName, "quickCreate", FormId);
        Assert.Equal(expected, FormXmlLocator.Locate(_root, entitySchemaName: null, formType: null, FormId));
    }

    [Fact]
    public void Locate_NothingKnown_ReturnsNewestForm()
    {
        AddForm("udpp_other", "main", "00000000-0000-0000-0000-0000000000aa", DateTime.Now.AddMinutes(-10));
        var expected = AddForm(EntityName, "main", FormId, DateTime.Now);
        Assert.Equal(expected, FormXmlLocator.Locate(_root, entitySchemaName: null, formType: null, formId: null));
    }

    [Fact]
    public void Locate_FormIdUnknown_ReturnsNewestInFormDirectory()
    {
        AddForm(EntityName, "main", "00000000-0000-0000-0000-0000000000aa", DateTime.Now.AddMinutes(-10));
        var expected = AddForm(EntityName, "main", FormId, DateTime.Now);
        Assert.Equal(expected, FormXmlLocator.Locate(_root, EntityName, "main", formId: null));
    }

    [Fact]
    public void Locate_DialogFormType_ResolvesDialogsDirectory()
    {
        var dialogs = Path.Combine(_root, "Dialogs");
        Directory.CreateDirectory(dialogs);
        var expected = Path.Combine(dialogs, $"{{{FormId}}}.xml");
        File.WriteAllText(expected, "<form />");
        Assert.Equal(expected, FormXmlLocator.Locate(_root, EntityName, "dialog", FormId));
    }
}
