using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Tests;

public class MetadataContractTests
{
    [Fact]
    public void LocalizedMetadata_UsesDisplayNameInsteadOfLocalizedNameProperty()
    {
        var workflow = new WorkflowMetadata { WorkflowId = "wf-1" };
        var view = new SavedQueryMetadata { SavedQueryId = "view-1" };
        var form = new FormMetadata { FormId = "form-1" };

        workflow.DisplayName = new Label("Workflow");
        view.DisplayName = new Label("View");
        form.DisplayName = new Label("Form");

        Assert.Equal("Workflow", workflow.DisplayName.Default);
        Assert.Equal("View", view.DisplayName.Default);
        Assert.Equal("Form", form.DisplayName.Default);
    }

    [Fact]
    public void SharedInterfaces_AreImplementedByRepresentativeMetadataTypes()
    {
        Assert.IsAssignableFrom<ILocalizedMetadata>(new EntityMetadata { LogicalName = "account" });
        Assert.IsAssignableFrom<ILocalizedMetadata>(new WorkflowMetadata { WorkflowId = "wf-1" });
        Assert.IsAssignableFrom<ILocalizedMetadata>(new SavedQueryMetadata { SavedQueryId = "view-1" });
        Assert.IsAssignableFrom<IDisplayNamedMetadata>(new AppModuleMetadata { UniqueName = "app" });
        Assert.IsAssignableFrom<IVersionedMetadata>(new SecurityRoleMetadata { RoleId = "role-1", Name = "Role" });
        Assert.IsAssignableFrom<ICustomizableMetadata>(new SdkMessageProcessingStepMetadata { SdkMessageProcessingStepId = "step-1" });
        Assert.IsAssignableFrom<IDeletableMetadata>(new WebResourceMetadata { WebResourceId = "wr-1", Name = "sample.js" });
        Assert.IsAssignableFrom<ILocalizedMetadata>(new Solution { UniqueName = "Solution" });
        Assert.IsAssignableFrom<ILocalizedMetadata>(new Publisher { UniqueName = "publisher", Prefix = "pub" });
    }
}
