using System.IO.Compression;
using TALXIS.Platform.Metadata.Packaging;

namespace TALXIS.Platform.Metadata.Tests;

// Warnings are written in SolutionPackagerLib's exact text format, since that is what the filter parses.
public class PackedFormRootComponentFilterTests
{
    private const string PackedFormId = "{21f0d212-90a2-43a2-8520-7f3d59c6be1d}";
    private const string QuickFormId = "{0b5e3a0c-58d3-4c65-9e5c-2a3f5b8f6d11}";
    private const string MissingFormId = "{7f1cbebe-4ae9-4e9a-9be4-44c6ec55addc}";
    private const string MissingStepId = "{35c5f395-d4b2-45b5-8c26-1d250f6d0b72}";

    [Fact]
    public void Apply_OnlyPackedForms_RemovesWarning()
    {
        const string otherWarning = "Some unrelated packager warning";
        var warning = MissingRootWarning(("SystemForm", PackedFormId));

        var result = ApplyWithZip(EntityForms(PackedFormId.Trim('{', '}').ToUpperInvariant()), otherWarning, warning);

        Assert.False(result.HasMissingRootComponents);
        Assert.Equal([otherWarning], result.Warnings);
    }

    [Fact]
    public void Apply_PackedFormNextToMissingComponents_KeepsOnlyMissingOnes()
    {
        // A key may contain a quote: schema names in Solution.xml are not validated.
        var warning = MissingRootWarning(
            ("SystemForm", PackedFormId),
            ("SdkMessageProcessingStep", MissingStepId),
            ("SystemForm", MissingFormId),
            ("WebResource", "WebResource-new_/it's.js"));

        var result = ApplyWithZip(EntityForms(PackedFormId), warning);

        Assert.Equal(
            [
                new MissingRootComponent("SdkMessageProcessingStep", MissingStepId),
                new MissingRootComponent("SystemForm", MissingFormId),
                new MissingRootComponent("WebResource", "WebResource-new_/it's.js"),
            ],
            result.MissingRootComponents);
        Assert.Equal(result.MissingRootComponentWarnings, result.Warnings);
    }

    [Fact]
    public void Apply_RowInUnexpectedFormat_KeepsWarningUnchanged()
    {
        var warning = MissingRootWarning(("SystemForm", PackedFormId)) + "  Some row in another format\r\n";

        var result = ApplyWithZip(EntityForms(PackedFormId), warning);

        Assert.Equal([warning], result.MissingRootComponentWarnings);
        Assert.Equal([warning], result.Warnings);
    }

    [Fact]
    public void Apply_ReadsFormIdsOnlyFromEntityForms()
    {
        // Dashboards are matched by the packager itself, so the same id elsewhere must not hide a missing form.
        var warning = MissingRootWarning(("SystemForm", PackedFormId), ("SystemForm", QuickFormId), ("SystemForm", MissingFormId));
        var customizations = $"""
            <ImportExportXml>
              <Dashboards>
                <Dashboard><FormId>{MissingFormId}</FormId></Dashboard>
              </Dashboards>
              <Entities>
                <Entity>
                  <FormXml>
                    <forms type="main"><systemform><formid>{PackedFormId}</formid></systemform></forms>
                  </FormXml>
                  <systemform><formid>{MissingFormId}</formid></systemform>
                </Entity>
                <Entity>
                  <FormXml>
                    <forms type="quick"><systemform><formid>{QuickFormId}</formid></systemform></forms>
                  </FormXml>
                </Entity>
              </Entities>
            </ImportExportXml>
            """;

        var result = ApplyWithZip(customizations, warning);

        Assert.Equal([new MissingRootComponent("SystemForm", MissingFormId)], result.MissingRootComponents);
    }

    [Fact]
    public void Apply_UnreadableCustomizations_KeepsWarningUnchanged()
    {
        var warning = MissingRootWarning(("SystemForm", PackedFormId));

        var result = ApplyWithZip(EntityForms(PackedFormId)[..^40], warning);

        Assert.Equal([warning], result.MissingRootComponentWarnings);
        Assert.StartsWith("Could not read customizations.xml", result.Warnings[^1]);
    }

    private static SolutionPackagerResult ApplyWithZip(string customizationsXml, params string[] warnings)
    {
        var zipPath = Path.Combine(Path.GetTempPath(), $"packed-form-{Guid.NewGuid():N}.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        using (var writer = new StreamWriter(archive.CreateEntry("customizations.xml").Open()))
        {
            writer.Write(customizationsXml);
        }

        try
        {
            return PackedFormRootComponentFilter.Apply(new SolutionPackagerResult([], warnings), zipPath);
        }
        finally
        {
            File.Delete(zipPath);
        }
    }

    private static string MissingRootWarning(params (string Type, string Key)[] components)
    {
        return SolutionPackagerResult.MissingRootComponentsWarningPrefix + ":\r\n"
            + string.Concat(components.Select(c => $"  Type='{c.Type}', Id (or schema name)='{c.Key}'.\r\n"));
    }

    private static string EntityForms(string formId)
    {
        return $"""
            <ImportExportXml>
              <Entities>
                <Entity>
                  <FormXml>
                    <forms type="main">
                      <systemform>
                        <formid>{formId}</formid>
                      </systemform>
                    </forms>
                  </FormXml>
                </Entity>
              </Entities>
            </ImportExportXml>
            """;
    }
}
