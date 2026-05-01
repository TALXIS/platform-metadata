using TALXIS.Platform.Metadata;

namespace TALXIS.Platform.Metadata.Tests;

public class LabelTests
{
    [Fact]
    public void DefaultConstructor_CreatesEmptyLabel()
    {
        var label = new Label();

        Assert.Null(label.Default);
        Assert.Empty(label.LocalizedLabels);
    }

    [Fact]
    public void Constructor_WithText_DefaultsToEnglish1033()
    {
        var label = new Label("Account");

        Assert.Equal("Account", label[1033]);
        Assert.Equal("Account", label.Default);
    }

    [Fact]
    public void Constructor_WithExplicitLanguageCode()
    {
        var label = new Label("Konto", 1031); // German

        Assert.Equal("Konto", label[1031]);
        Assert.Null(label[1033]);
    }

    [Fact]
    public void MultiLanguageLabel()
    {
        var label = new Label("Account");
        label[1031] = "Konto";    // German
        label[1036] = "Compte";   // French

        Assert.Equal("Account", label[1033]);
        Assert.Equal("Konto", label[1031]);
        Assert.Equal("Compte", label[1036]);
        Assert.Equal(3, label.LocalizedLabels.Count);
    }

    [Fact]
    public void Default_ReturnsEnglish_WhenAvailable()
    {
        var label = new Label();
        label[1031] = "Konto";
        label[1033] = "Account";

        Assert.Equal("Account", label.Default);
    }

    [Fact]
    public void Default_ReturnsFirstAvailable_WhenEnglishMissing()
    {
        var label = new Label("Konto", 1031);

        Assert.Equal("Konto", label.Default);
    }

    [Fact]
    public void Indexer_UnknownLanguage_ReturnsNull()
    {
        var label = new Label("Account");

        Assert.Null(label[9999]);
    }

    [Fact]
    public void SetNull_RemovesLanguageEntry()
    {
        var label = new Label("Account");
        Assert.Equal("Account", label[1033]);

        label[1033] = null;

        Assert.Null(label[1033]);
        Assert.Empty(label.LocalizedLabels);
    }

    [Fact]
    public void SetNull_OnNonExistent_DoesNotThrow()
    {
        var label = new Label();
        label[9999] = null; // should not throw
        Assert.Empty(label.LocalizedLabels);
    }

    [Fact]
    public void ToString_ReturnsDefault()
    {
        var label = new Label("Account");
        Assert.Equal("Account", label.ToString());
    }

    [Fact]
    public void ToString_EmptyLabel_ReturnsEmptyString()
    {
        var label = new Label();
        Assert.Equal("", label.ToString());
    }

    [Fact]
    public void Overwrite_ExistingLanguage_UpdatesValue()
    {
        var label = new Label("Old");
        label[1033] = "New";

        Assert.Equal("New", label[1033]);
        Assert.Single(label.LocalizedLabels);
    }
}
