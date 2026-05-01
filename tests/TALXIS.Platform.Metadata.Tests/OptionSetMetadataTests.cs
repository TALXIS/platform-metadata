using TALXIS.Platform.Metadata;
using TALXIS.Platform.Metadata.Components;

namespace TALXIS.Platform.Metadata.Tests;

public class OptionSetMetadataTests
{
    [Fact]
    public void Create_WithRequiredProperties()
    {
        var optionSet = new OptionSetMetadata { Name = "status" };

        Assert.Equal("status", optionSet.Name);
        Assert.Empty(optionSet.Options);
        Assert.NotNull(optionSet.DisplayName);
        Assert.NotNull(optionSet.Description);
    }

    [Fact]
    public void AddOption_AddsToCollection()
    {
        var optionSet = new OptionSetMetadata { Name = "status" };
        var option = new OptionMetadata { Value = 1 };
        option.Label[1033] = "Active";

        optionSet.AddOption(option);

        Assert.Single(optionSet.Options);
        Assert.Equal(1, optionSet.Options[0].Value);
        Assert.Equal("Active", optionSet.Options[0].Label[1033]);
    }

    [Fact]
    public void AddOption_MultipleOptions_AllPresent()
    {
        var optionSet = new OptionSetMetadata { Name = "priority" };
        optionSet.AddOption(new OptionMetadata { Value = 1 });
        optionSet.AddOption(new OptionMetadata { Value = 2 });
        optionSet.AddOption(new OptionMetadata { Value = 3 });

        Assert.Equal(3, optionSet.Options.Count);
    }

    [Fact]
    public void RemoveOption_ByValue_RemovesCorrectOption()
    {
        var optionSet = new OptionSetMetadata { Name = "status" };
        optionSet.AddOption(new OptionMetadata { Value = 1 });
        optionSet.AddOption(new OptionMetadata { Value = 2 });
        optionSet.AddOption(new OptionMetadata { Value = 3 });

        optionSet.RemoveOption(2);

        Assert.Equal(2, optionSet.Options.Count);
        Assert.DoesNotContain(optionSet.Options, o => o.Value == 2);
    }

    [Fact]
    public void RemoveOption_NonExistentValue_DoesNothing()
    {
        var optionSet = new OptionSetMetadata { Name = "status" };
        optionSet.AddOption(new OptionMetadata { Value = 1 });

        optionSet.RemoveOption(999);

        Assert.Single(optionSet.Options);
    }

    [Fact]
    public void Options_AreReadable()
    {
        var optionSet = new OptionSetMetadata { Name = "category" };
        var opt1 = new OptionMetadata { Value = 100 };
        opt1.Label[1033] = "Standard";
        var opt2 = new OptionMetadata { Value = 200 };
        opt2.Label[1033] = "Premium";

        optionSet.AddOption(opt1);
        optionSet.AddOption(opt2);

        Assert.Equal(100, optionSet.Options[0].Value);
        Assert.Equal("Standard", optionSet.Options[0].Label[1033]);
        Assert.Equal(200, optionSet.Options[1].Value);
        Assert.Equal("Premium", optionSet.Options[1].Label[1033]);
    }

    [Fact]
    public void IsGlobal_DefaultFalse()
    {
        var optionSet = new OptionSetMetadata { Name = "local_set" };
        Assert.False(optionSet.IsGlobal);
    }

    [Fact]
    public void BooleanOptionSetMetadata_HasTrueAndFalseLabels()
    {
        var boolSet = new BooleanOptionSetMetadata();
        boolSet.TrueLabel[1033] = "Yes";
        boolSet.FalseLabel[1033] = "No";

        Assert.Equal("Yes", boolSet.TrueLabel[1033]);
        Assert.Equal("No", boolSet.FalseLabel[1033]);
    }
}
