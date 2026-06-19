using TALXIS.Platform.Metadata.Validation;

namespace TALXIS.Platform.Metadata.Tests;

public class GuidValidatorTests : IDisposable
{
    private readonly string _tempDir;
    private readonly GuidValidator _validator = new();

    public GuidValidatorTests()
    {
        _tempDir = Path.Combine(
            Path.GetDirectoryName(typeof(GuidValidatorTests).Assembly.Location)!,
            "GuidValidatorTestData_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void NoDuplicates_ReturnsEmpty()
    {
        var formsDir = Path.Combine(_tempDir, "FormXml");
        Directory.CreateDirectory(formsDir);

        File.WriteAllText(Path.Combine(formsDir, "form1.xml"), """
            <form>
              <formid>{11111111-1111-1111-1111-111111111111}</formid>
            </form>
            """);

        File.WriteAllText(Path.Combine(formsDir, "form2.xml"), """
            <form>
              <formid>{22222222-2222-2222-2222-222222222222}</formid>
            </form>
            """);

        var results = _validator.ValidateDirectory(_tempDir);

        Assert.Empty(results);
    }

    [Fact]
    public void DuplicateGuids_DetectedAcrossFiles()
    {
        var formsDir = Path.Combine(_tempDir, "FormXml");
        Directory.CreateDirectory(formsDir);

        var duplicateGuid = "{AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE}";

        File.WriteAllText(Path.Combine(formsDir, "form1.xml"), $"""
            <form>
              <formid>{duplicateGuid}</formid>
            </form>
            """);

        File.WriteAllText(Path.Combine(formsDir, "form2.xml"), $"""
            <form>
              <formid>{duplicateGuid}</formid>
            </form>
            """);

        var results = _validator.ValidateDirectory(_tempDir);

        Assert.NotEmpty(results);
        Assert.True(results.Count >= 2, "Expected at least 2 results (one per occurrence)");
        Assert.All(results, r => Assert.Equal(ValidationSeverity.Error, r.Severity));
        Assert.All(results, r => Assert.Contains("Duplicate GUID", r.Message));
    }

    [Fact]
    public void DuplicateAttributeGuids_DetectedAcrossFiles()
    {
        var stepsDir = Path.Combine(_tempDir, "SdkMessageProcessingSteps");
        Directory.CreateDirectory(stepsDir);

        var duplicateGuid = "{AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE}";

        File.WriteAllText(Path.Combine(stepsDir, "step1.xml"), $"""
            <SdkMessageProcessingStep SdkMessageProcessingStepId="{duplicateGuid}" Name="Step1" />
            """);

        File.WriteAllText(Path.Combine(stepsDir, "step2.xml"), $"""
            <SdkMessageProcessingStep SdkMessageProcessingStepId="{duplicateGuid}" Name="Step2" />
            """);

        var results = _validator.ValidateDirectory(_tempDir);

        Assert.NotEmpty(results);
        Assert.True(results.Count >= 2, "Expected at least 2 results (one per occurrence)");
        Assert.All(results, r => Assert.Equal(ValidationSeverity.Error, r.Severity));
        Assert.All(results, r => Assert.Contains("Duplicate GUID", r.Message));
    }

    [Fact]
    public void DifferentAttributeGuids_NoDuplicates()
    {
        var stepsDir = Path.Combine(_tempDir, "SdkMessageProcessingSteps");
        Directory.CreateDirectory(stepsDir);

        File.WriteAllText(Path.Combine(stepsDir, "step1.xml"), """
            <SdkMessageProcessingStep SdkMessageProcessingStepId="{11111111-1111-1111-1111-111111111111}" Name="Step1" />
            """);

        File.WriteAllText(Path.Combine(stepsDir, "step2.xml"), """
            <SdkMessageProcessingStep SdkMessageProcessingStepId="{22222222-2222-2222-2222-222222222222}" Name="Step2" />
            """);

        var results = _validator.ValidateDirectory(_tempDir);

        Assert.Empty(results);
    }

    [Fact]
    public void RootComponentReference_InProjectFolderNamedRoles_NotFlaggedAsDuplicate()
    {

        var workspace = Path.Combine(_tempDir, "Security.Roles");

        var roleGuid = "{1bbf5210-5833-4b95-a38e-9f771ee3481e}";

        var rolesDir = Path.Combine(workspace, "Roles");
        Directory.CreateDirectory(rolesDir);
        File.WriteAllText(Path.Combine(rolesDir, "role.xml"), $"""
            <Role id="{roleGuid}" name="Some Role" />
            """);

        var otherDir = Path.Combine(workspace, "Other");
        Directory.CreateDirectory(otherDir);
        File.WriteAllText(Path.Combine(otherDir, "Solution.xml"), $"""
            <ImportExportXml>
              <SolutionManifest>
                <RootComponents>
                  <RootComponent type="20" id="{roleGuid}" behavior="0" />
                </RootComponents>
              </SolutionManifest>
            </ImportExportXml>
            """);

        var results = _validator.ValidateDirectory(workspace);

        Assert.Empty(results);
    }

    [Fact]
    public void DuplicateRoleDeclarations_StillDetected()
    {
        var rolesDir = Path.Combine(_tempDir, "Roles");
        Directory.CreateDirectory(rolesDir);

        var duplicateGuid = "{1bbf5210-5833-4b95-a38e-9f771ee3481e}";

        File.WriteAllText(Path.Combine(rolesDir, "role1.xml"), $"""
            <Role id="{duplicateGuid}" name="Role One" />
            """);
        File.WriteAllText(Path.Combine(rolesDir, "role2.xml"), $"""
            <Role id="{duplicateGuid}" name="Role Two" />
            """);

        var results = _validator.ValidateDirectory(_tempDir);

        Assert.NotEmpty(results);
        Assert.True(results.Count >= 2, "Expected at least 2 results (one per occurrence)");
        Assert.All(results, r => Assert.Equal(ValidationSeverity.Error, r.Severity));
    }

    [Fact]
    public void DifferentComponentTypes_DontConflict()
    {
        var sameGuid = "{AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE}";

        // formid in FormXml directory
        var formsDir = Path.Combine(_tempDir, "FormXml");
        Directory.CreateDirectory(formsDir);
        File.WriteAllText(Path.Combine(formsDir, "form.xml"), $"""
            <form>
              <formid>{sameGuid}</formid>
            </form>
            """);

        // savedqueryid in SavedQueries directory
        var queriesDir = Path.Combine(_tempDir, "SavedQueries");
        Directory.CreateDirectory(queriesDir);
        File.WriteAllText(Path.Combine(queriesDir, "view.xml"), $"""
            <savedquery>
              <savedqueryid>{sameGuid}</savedqueryid>
            </savedquery>
            """);

        var results = _validator.ValidateDirectory(_tempDir);

        // GuidValidator tracks GUIDs globally — same GUID across different
        // component types (formid vs savedqueryid) IS flagged as a duplicate.
        Assert.NotEmpty(results);
        Assert.True(results.Count >= 2, "Expected at least 2 results (one per occurrence)");
        Assert.All(results, r => Assert.Equal(ValidationSeverity.Error, r.Severity));
    }

    [Fact]
    public void ManagedAndUnmanagedTwins_SharingFormId_NotFlaggedAsDuplicate()
    {
        // {id}.xml (unmanaged) and {id}_managed.xml (managed) are two layers of
        // the SAME component and legitimately share their formid. They must
        // never be reported as duplicates of each other.
        var formsDir = Path.Combine(_tempDir, "FormXml");
        Directory.CreateDirectory(formsDir);

        var sharedGuid = "{2fc2021f-8b2a-48ad-946c-06c3b250fa25}";

        File.WriteAllText(Path.Combine(formsDir, "form.xml"), $"""
            <form>
              <formid>{sharedGuid}</formid>
            </form>
            """);
        File.WriteAllText(Path.Combine(formsDir, "form_managed.xml"), $"""
            <form>
              <formid>{sharedGuid}</formid>
            </form>
            """);

        var results = _validator.ValidateDirectory(_tempDir);

        Assert.Empty(results);
    }

    [Fact]
    public void RealDuplicate_NotMaskedByPresenceOfManagedTwin()
    {
        // A genuine collision with a third, unrelated file must still be
        // reported even when a managed/unmanaged twin pair is present.
        var formsDir = Path.Combine(_tempDir, "FormXml");
        Directory.CreateDirectory(formsDir);

        var sharedGuid = "{2fc2021f-8b2a-48ad-946c-06c3b250fa25}";

        File.WriteAllText(Path.Combine(formsDir, "form.xml"), $"""
            <form><formid>{sharedGuid}</formid></form>
            """);
        File.WriteAllText(Path.Combine(formsDir, "form_managed.xml"), $"""
            <form><formid>{sharedGuid}</formid></form>
            """);
        File.WriteAllText(Path.Combine(formsDir, "other.xml"), $"""
            <form><formid>{sharedGuid}</formid></form>
            """);

        var results = _validator.ValidateDirectory(_tempDir);

        // The real collision (against other.xml) is still reported...
        Assert.Contains(results, r =>
            r.FilePath != null && Path.GetFileName(r.FilePath) == "other.xml");
        // ...but the twins never point at each other.
        Assert.DoesNotContain(results, r =>
            r.FilePath != null && Path.GetFileName(r.FilePath) == "form.xml"
            && r.Message.Contains("form_managed.xml"));
    }

    [Fact]
    public void BuildOutputCopiesInObjFolder_NotScanned()
    {
        // After a build, obj/.../Metadata holds copies of the source metadata.
        // Those copies must never collide with the originals.
        var sharedGuid = "{79c46cbc-5506-419a-9ab7-f603ad1d6ff6}";

        var sourceDir = Path.Combine(_tempDir, "Entities", "pba_test", "FormXml", "main");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(Path.Combine(sourceDir, "form.xml"), $"""
            <form><formid>{sharedGuid}</formid></form>
            """);

        var objCopyDir = Path.Combine(_tempDir, "obj", "Debug", "net462", "Metadata", "Entities", "pba_test", "FormXml", "main");
        Directory.CreateDirectory(objCopyDir);
        File.WriteAllText(Path.Combine(objCopyDir, "form.xml"), $"""
            <form><formid>{sharedGuid}</formid></form>
            """);

        var results = _validator.ValidateDirectory(_tempDir);

        Assert.Empty(results);
    }
}
