using System.IO.Compression;
using TALXIS.Platform.Metadata.Packaging;

namespace TALXIS.Platform.Metadata.Tests;

public class PackedFormRootComponentFilterTests
{
    private const string PackedFormId = "{21f0d212-90a2-43a2-8520-7f3d59c6be1d}";
    private const string MissingFormId = "{7f1cbebe-4ae9-4e9a-9be4-44c6ec55addc}";
    private const string MissingStepId = "{35c5f395-d4b2-45b5-8c26-1d250f6d0b72}";

    [Fact]
    public void Apply_FormPackedInsideEntity_IsNotReportedAsMissing()
    {
        var zipPath = CreateZipWithForm(PackedFormId.ToUpperInvariant());
        var warning = MissingRootWarning(("SystemForm", PackedFormId));

        try
        {
            var result = PackedFormRootComponentFilter.Apply(new SolutionPackagerResult([], [warning]), zipPath);

            Assert.False(result.HasMissingRootComponents);
            Assert.Contains(warning, result.Warnings);
        }
        finally
        {
            File.Delete(zipPath);
        }
    }

    [Fact]
    public void Apply_FormNotPacked_StaysMissing()
    {
        var zipPath = CreateZipWithForm(PackedFormId);
        var warning = MissingRootWarning(("SystemForm", MissingFormId));

        try
        {
            var result = PackedFormRootComponentFilter.Apply(new SolutionPackagerResult([], [warning]), zipPath);

            Assert.Equal(warning, Assert.Single(result.MissingRootComponentWarnings));
        }
        finally
        {
            File.Delete(zipPath);
        }
    }

    [Fact]
    public void Apply_PackedFormNextToRealMissingComponents_KeepsOnlyRealOnes()
    {
        var zipPath = CreateZipWithForm(PackedFormId);
        var warning = MissingRootWarning(
            ("SystemForm", PackedFormId),
            ("SdkMessageProcessingStep", MissingStepId),
            ("SystemForm", MissingFormId));

        try
        {
            var result = PackedFormRootComponentFilter.Apply(new SolutionPackagerResult([], [warning]), zipPath);

            var remaining = Assert.Single(result.MissingRootComponentWarnings);
            Assert.StartsWith(SolutionPackagerResult.MissingRootComponentsWarningPrefix, remaining);
            Assert.DoesNotContain(PackedFormId, remaining);
            Assert.Contains(MissingStepId, remaining);
            Assert.Contains(MissingFormId, remaining);
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

    private static string CreateZipWithForm(string formId)
    {
        var zipPath = Path.Combine(Path.GetTempPath(), $"packed-form-{Guid.NewGuid():N}.zip");
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        using var writer = new StreamWriter(archive.CreateEntry("customizations.xml").Open());
        writer.Write($"""
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
            """);
        return zipPath;
    }
}
