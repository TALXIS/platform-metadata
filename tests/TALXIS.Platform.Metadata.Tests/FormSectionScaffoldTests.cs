using System.Xml.Linq;
using TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

namespace TALXIS.Platform.Metadata.Tests;

public class FormSectionScaffoldTests : IDisposable
{
    private const string EntityName = "udpp_warehouseitem";
    private const string FormId = "a1b2c3d4-e5f6-4a1b-8c2d-000000000001";
    private const string SectionId = "a1b2c3d4-e5f6-4a1b-8c2d-0000000000b1";

    private readonly string _root = Directory.CreateTempSubdirectory("metadata-form-section-scaffold").FullName;
    private readonly string _formFilePath;
    private readonly string _sectionGivenIdFilePath;
    private readonly string _sectionUnknownIdFilePath;

    public FormSectionScaffoldTests()
    {
        var formDirectory = Path.Combine(_root, "Entities", EntityName, "FormXml", "main");
        Directory.CreateDirectory(formDirectory);
        _formFilePath = Path.Combine(formDirectory, $"{{{FormId}}}.xml");
        File.WriteAllText(_formFilePath, """
            <form>
              <tabs>
                <tab id="{00000000-0000-0000-0000-0000000000a1}">
                  <columns>
                    <column width="50%" />
                    <column width="50%">
                      <sections>
                        <section id="{00000000-0000-0000-0000-0000000000c1}" />
                      </sections>
                    </column>
                  </columns>
                </tab>
              </tabs>
            </form>
            """);

        // The rendered fragment carries the raw id parameter and a literal name token;
        // these are filled with the final id and normalized name.
        _sectionGivenIdFilePath = Path.Combine(_root, "section-given.xml");
        File.WriteAllText(_sectionGivenIdFilePath, $$"""
            <section showlabel="true" showbar="false" IsUserDefined="0" id="{{{SectionId}}}"
              labelid="{00000000-0000-0000-0000-0000000000d1}" name="examplesectionname">
              <labels>
                <label description="Extra Details" languagecode="1033" />
              </labels>
              <rows>
              </rows>
            </section>
            """);
        _sectionUnknownIdFilePath = Path.Combine(_root, "section-unknown.xml");
        File.WriteAllText(_sectionUnknownIdFilePath, """
            <section showlabel="true" showbar="false" IsUserDefined="0" id="{unknown}"
              labelid="{00000000-0000-0000-0000-0000000000d2}" name="examplesectionname">
              <labels>
                <label description="GENERAL" languagecode="1033" />
              </labels>
              <rows>
              </rows>
            </section>
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private FormSectionScaffoldRequest Request(string fragmentPath) => new()
    {
        SolutionRootPath = _root,
        EntitySchemaName = EntityName,
        FormType = "main",
        FormId = FormId,
        SectionName = "Extra Details",
        SectionFilePath = fragmentPath,
    };

    [Fact]
    public void Apply_GivenSectionId_AppendsWithNormalizedName()
    {
        var request = Request(_sectionGivenIdFilePath);
        request.SectionId = SectionId;
        var result = FormSectionScaffold.Apply(request);

        Assert.Empty(result.Warnings);
        var doc = XDocument.Load(_formFilePath);
        var section = doc.Descendants("section").Single(s => s.Attribute("id")?.Value == $"{{{SectionId}}}");
        Assert.Equal("extradetails", section.Attribute("name")?.Value);
    }

    [Fact]
    public void Apply_UnknownSectionId_GeneratesGuid()
    {
        var request = Request(_sectionUnknownIdFilePath);
        request.SectionName = "GENERAL";
        FormSectionScaffold.Apply(request);

        var doc = XDocument.Load(_formFilePath);
        var lastColumn = doc.Descendants("column").Last();
        var added = lastColumn.Descendants("section").Single(s => s.Attribute("name")?.Value == "general");
        Assert.True(Guid.TryParse(added.Attribute("id")?.Value.Trim('{', '}'), out _));
    }

    [Fact]
    public void Apply_ColumnWithoutSections_CreatesContainer()
    {
        var request = Request(_sectionGivenIdFilePath);
        request.SectionId = SectionId;
        request.Placement.ColumnIndex = "1";
        FormSectionScaffold.Apply(request);

        var doc = XDocument.Load(_formFilePath);
        var firstColumn = doc.Descendants("column").First();
        Assert.Single(firstColumn.Element("sections")!.Elements("section"));
    }

    [Fact]
    public void Apply_ViaComponentScaffold_DispatchesFormSection()
    {
        var result = ComponentScaffold.Apply(new ComponentScaffoldRequest
        {
            ComponentType = "FormSection",
            SolutionRootPath = _root,
            Files = new Dictionary<string, string> { ["section"] = _sectionUnknownIdFilePath },
            Parameters = new Dictionary<string, string>
            {
                ["entity"] = EntityName,
                ["form-type"] = "main",
                ["form-id"] = FormId,
                ["section-id"] = "unknown",
                ["section-name"] = "GENERAL",
            },
        });

        Assert.Empty(result.Warnings);
        var doc = XDocument.Load(_formFilePath);
        Assert.Single(doc.Descendants("section"), s => s.Attribute("name")?.Value == "general");
    }
}
