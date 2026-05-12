using System.Threading;
using TALXIS.Platform.Metadata;

namespace TALXIS.Platform.Metadata.Tests;

public class ComponentDefinitionRegistryTests
{
    private static int _customTypeSeed = 100000;

    [Theory]
    [InlineData(ComponentType.Entity, 1)]
    [InlineData(ComponentType.Attribute, 2)]
    [InlineData(ComponentType.OptionSet, 9)]
    [InlineData(ComponentType.Role, 20)]
    [InlineData(ComponentType.SavedQuery, 26)]
    [InlineData(ComponentType.Workflow, 29)]
    [InlineData(ComponentType.SystemForm, 60)]
    [InlineData(ComponentType.WebResource, 61)]
    [InlineData(ComponentType.SiteMap, 62)]
    [InlineData(ComponentType.PluginAssembly, 91)]
    [InlineData(ComponentType.SdkMessageProcessingStep, 92)]
    [InlineData(ComponentType.AppModule, 80)]
    [InlineData(ComponentType.CanvasApp, 300)]
    public void CommonTypeCodesHaveCorrectValues(ComponentType type, int expected)
    {
        Assert.Equal(expected, (int)type);
    }

    [Theory]
    [InlineData(ComponentType.Entity, "Entities", "$(PrimaryName)/Entity.xml", IdentityStrategy.Name)]
    [InlineData(ComponentType.WebResource, "WebResources", "$(PrimaryName)", IdentityStrategy.Guid)]
    [InlineData(ComponentType.SiteMap, "Other", "$(type)$(managed).xml", IdentityStrategy.Singleton)]
    [InlineData(ComponentType.PluginAssembly, "PluginAssemblies", "PluginAssemblies.xml", IdentityStrategy.Guid)]
    [InlineData(ComponentType.Workflow, "Workflows", "Workflows.xml", IdentityStrategy.Guid)]
    [InlineData(ComponentType.Role, "Roles", "$(PrimaryName)", IdentityStrategy.Guid)]
    public void GetByType_ReturnsCorrectDefinition(ComponentType type, string directory, string filePattern, IdentityStrategy identity)
    {
        var def = ComponentDefinitionRegistry.GetByType(type);

        Assert.NotNull(def);
        Assert.Equal(type, def.TypeCode);
        Assert.Equal(directory, def.Directory);
        Assert.Equal(filePattern, def.FilePattern);
        Assert.Equal(identity, def.Identity);
    }

    [Theory]
    [InlineData("Entities", ComponentType.Entity)]
    [InlineData("WebResources", ComponentType.WebResource)]
    [InlineData("SiteMap", ComponentType.SiteMap)]
    [InlineData("SolutionPluginAssemblies", ComponentType.PluginAssembly)]
    [InlineData("Roles", ComponentType.Role)]
    public void GetBySerializedName_ReturnsCorrectDefinition(string serializedName, ComponentType expectedType)
    {
        var def = ComponentDefinitionRegistry.GetBySerializedName(serializedName);

        Assert.NotNull(def);
        Assert.Equal(expectedType, def.TypeCode);
    }

    [Theory]
    [InlineData("entities")]
    [InlineData("ENTITIES")]
    [InlineData("Entities")]
    public void GetBySerializedName_IsCaseInsensitive(string serializedName)
    {
        var def = ComponentDefinitionRegistry.GetBySerializedName(serializedName);

        Assert.NotNull(def);
        Assert.Equal(ComponentType.Entity, def.TypeCode);
    }

    [Fact]
    public void GetByType_UnknownType_ReturnsNull()
    {
        var def = ComponentDefinitionRegistry.GetByType((ComponentType)(-1));

        Assert.Null(def);
    }

    [Fact]
    public void GetBySerializedName_UnknownElement_ReturnsNull()
    {
        var def = ComponentDefinitionRegistry.GetBySerializedName("NonExistentElement");

        Assert.Null(def);
    }

    [Fact]
    public void GetAll_ReturnsNonEmptyCollection()
    {
        var all = ComponentDefinitionRegistry.GetAll().ToList();

        Assert.NotEmpty(all);
        Assert.True(all.Count >= 30, $"Expected at least 30 registered definitions, got {all.Count}");
    }

    [Fact]
    public void Entity_HasSupportsMerge_And_HasSubfolders()
    {
        var def = ComponentDefinitionRegistry.GetByType(ComponentType.Entity);

        Assert.NotNull(def);
        Assert.True(def.SupportsMerge);
        Assert.True(def.HasSubfolders);
    }

    [Fact]
    public void WebResource_HasIsFileBacked()
    {
        var def = ComponentDefinitionRegistry.GetByType(ComponentType.WebResource);

        Assert.NotNull(def);
        Assert.True(def.IsFileBacked);
    }

    [Fact]
    public void CanvasApp_HasIsFileBacked()
    {
        var def = ComponentDefinitionRegistry.GetByType(ComponentType.CanvasApp);

        Assert.NotNull(def);
        Assert.True(def.IsFileBacked);
    }

    [Fact]
    public void SiteMap_HasSingletonIdentityStrategy()
    {
        var def = ComponentDefinitionRegistry.GetByType(ComponentType.SiteMap);

        Assert.NotNull(def);
        Assert.Equal(IdentityStrategy.Singleton, def.Identity);
    }

    [Fact]
    public void AppModule_HasSupportsMerge_And_HasSubfolders()
    {
        var def = ComponentDefinitionRegistry.GetByType(ComponentType.AppModule);

        Assert.NotNull(def);
        Assert.True(def.SupportsMerge);
        Assert.True(def.HasSubfolders);
    }

    [Fact]
    public void EntityMap_HasCompositeIdentityStrategy()
    {
        var def = ComponentDefinitionRegistry.GetByType(ComponentType.EntityMap);

        Assert.NotNull(def);
        Assert.Equal(IdentityStrategy.Composite, def.Identity);
    }

    [Fact]
    public void GenericComponent_HasProbedIdentityStrategy()
    {
        var def = ComponentDefinitionRegistry.GetByType(ComponentType.GenericComponent);

        Assert.NotNull(def);
        Assert.Equal(IdentityStrategy.Probed, def.Identity);
    }

    [Fact]
    public void Register_AllowsExternalDefinitions()
    {
        var typeCode = NextCustomTypeCode();
        var serializedName = $"ExternalDefinition_{Guid.NewGuid():N}";
        var definition = new ComponentDefinition(
            typeCode,
            "ExternalDefinition",
            serializedName,
            "External",
            "External.xml",
            IdentityStrategy.Name);

        ComponentDefinitionRegistry.Register(definition);

        Assert.Same(definition, ComponentDefinitionRegistry.GetByType(typeCode));
        Assert.Same(definition, ComponentDefinitionRegistry.GetBySerializedName(serializedName));
    }

    [Fact]
    public void Register_DuplicateTypeWithoutReplace_Throws()
    {
        var existing = ComponentDefinitionRegistry.GetByType(ComponentType.Entity)!;
        var duplicate = existing with { SerializedName = $"Entity_{Guid.NewGuid():N}" };

        var ex = Assert.Throws<InvalidOperationException>(() => ComponentDefinitionRegistry.Register(duplicate));
        Assert.Contains("already registered", ex.Message);
    }

    [Fact]
    public void Register_WithReplaceExisting_UpdatesLookups()
    {
        var typeCode = NextCustomTypeCode();
        var original = new ComponentDefinition(
            typeCode,
            "OriginalDefinition",
            $"Original_{Guid.NewGuid():N}",
            "External",
            "Original.xml",
            IdentityStrategy.Name);
        var replacement = original with
        {
            Name = "ReplacementDefinition",
            SerializedName = $"Replacement_{Guid.NewGuid():N}",
            FilePattern = "Replacement.xml"
        };

        ComponentDefinitionRegistry.Register(original);
        ComponentDefinitionRegistry.Register(replacement, replaceExisting: true);

        Assert.Same(replacement, ComponentDefinitionRegistry.GetByType(typeCode));
        Assert.Null(ComponentDefinitionRegistry.GetBySerializedName(original.SerializedName));
        Assert.Same(replacement, ComponentDefinitionRegistry.GetBySerializedName(replacement.SerializedName));
    }

    [Theory]
    [InlineData("", "SerializedName")]
    [InlineData("   ", "SerializedName")]
    public void Register_InvalidSerializedName_ThrowsArgumentException(string serializedName, string _)
    {
        var definition = new ComponentDefinition(
            NextCustomTypeCode(),
            "ExternalDefinition",
            serializedName,
            "External",
            "External.xml",
            IdentityStrategy.Name);

        var ex = Assert.Throws<ArgumentException>(() => ComponentDefinitionRegistry.Register(definition));
        Assert.Equal("def", ex.ParamName);
    }

    [Fact]
    public void GetAll_ReturnsSnapshot()
    {
        var before = ComponentDefinitionRegistry.GetAll().ToArray();
        var definition = new ComponentDefinition(
            NextCustomTypeCode(),
            "SnapshotDefinition",
            $"Snapshot_{Guid.NewGuid():N}",
            "External",
            "Snapshot.xml",
            IdentityStrategy.Name);

        ComponentDefinitionRegistry.Register(definition);

        Assert.DoesNotContain(before, item => item.TypeCode == definition.TypeCode);
        Assert.Contains(ComponentDefinitionRegistry.GetAll(), item => item.TypeCode == definition.TypeCode);
    }

    // ── GetByName tests ──

    [Theory]
    [InlineData("Entity", ComponentType.Entity)]
    [InlineData("Attribute", ComponentType.Attribute)]
    [InlineData("SavedQuery", ComponentType.SavedQuery)]
    [InlineData("Workflow", ComponentType.Workflow)]
    [InlineData("Role", ComponentType.Role)]
    [InlineData("SystemForm", ComponentType.SystemForm)]
    public void GetByName_CanonicalName_ReturnsDefinition(string name, ComponentType expected)
    {
        var def = ComponentDefinitionRegistry.GetByName(name);
        Assert.NotNull(def);
        Assert.Equal(expected, def.TypeCode);
    }

    [Theory]
    [InlineData("Table", ComponentType.Entity)]
    [InlineData("Column", ComponentType.Attribute)]
    [InlineData("Choice", ComponentType.OptionSet)]
    [InlineData("View", ComponentType.SavedQuery)]
    [InlineData("SecurityRole", ComponentType.Role)]
    [InlineData("Flow", ComponentType.Workflow)]
    [InlineData("Process", ComponentType.Workflow)]
    [InlineData("PluginStep", ComponentType.SdkMessageProcessingStep)]
    [InlineData("EnvironmentVariable", ComponentType.EnvironmentVariableDefinition)]
    [InlineData("PcfControl", ComponentType.CustomControl)]
    public void GetByName_Alias_ReturnsDefinition(string alias, ComponentType expected)
    {
        var def = ComponentDefinitionRegistry.GetByName(alias);
        Assert.NotNull(def);
        Assert.Equal(expected, def.TypeCode);
    }

    [Theory]
    [InlineData("entity")]
    [InlineData("ENTITY")]
    [InlineData("table")]
    [InlineData("TABLE")]
    [InlineData("flow")]
    [InlineData("SECURITYROLE")]
    public void GetByName_IsCaseInsensitive(string name)
    {
        var def = ComponentDefinitionRegistry.GetByName(name);
        Assert.NotNull(def);
    }

    [Theory]
    [InlineData("1", ComponentType.Entity)]
    [InlineData("60", ComponentType.SystemForm)]
    [InlineData("29", ComponentType.Workflow)]
    public void GetByName_IntegerString_ReturnsDefinition(string code, ComponentType expected)
    {
        var def = ComponentDefinitionRegistry.GetByName(code);
        Assert.NotNull(def);
        Assert.Equal(expected, def.TypeCode);
    }

    [Theory]
    [InlineData("Bot")]
    [InlineData("Dataflow")]
    public void GetByName_EnumName_WithoutRegisteredDefinition_ReturnsNull(string enumName)
    {
        // These enum values exist but don't have registered ComponentDefinitions.
        // GetByName falls through to Enum.TryParse → GetByType, which returns null if unregistered.
        var def = ComponentDefinitionRegistry.GetByName(enumName);
        Assert.Null(def);
    }

    [Fact]
    public void GetByName_EnumName_WithRegisteredDefinition_Returns()
    {
        // CanvasApp has a registered definition
        var def = ComponentDefinitionRegistry.GetByName("CanvasApp");
        Assert.NotNull(def);
        Assert.Equal(ComponentType.CanvasApp, def.TypeCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("NonExistentType")]
    [InlineData("99999999")]
    public void GetByName_InvalidInput_ReturnsNull(string? name)
    {
        var def = ComponentDefinitionRegistry.GetByName(name);
        Assert.Null(def);
    }

    [Fact]
    public void Register_WithAliases_PopulatesNameLookup()
    {
        var typeCode = NextCustomTypeCode();
        var definition = new ComponentDefinition(
            typeCode,
            "AliasedComponent",
            $"Aliased_{Guid.NewGuid():N}",
            "External",
            "Aliased.xml",
            IdentityStrategy.Name,
            Aliases: new[] { "AliasOne", "AliasTwo" });

        ComponentDefinitionRegistry.Register(definition);

        Assert.Same(definition, ComponentDefinitionRegistry.GetByName("AliasedComponent"));
        Assert.Same(definition, ComponentDefinitionRegistry.GetByName("AliasOne"));
        Assert.Same(definition, ComponentDefinitionRegistry.GetByName("AliasTwo"));
        Assert.Same(definition, ComponentDefinitionRegistry.GetByName("aliasone")); // case-insensitive
    }

    [Fact]
    public void Register_DuplicateAlias_Throws()
    {
        var typeCode = NextCustomTypeCode();
        var definition = new ComponentDefinition(
            typeCode,
            $"DuplicateAliasTest_{Guid.NewGuid():N}",
            $"DupAlias_{Guid.NewGuid():N}",
            "External",
            "DupAlias.xml",
            IdentityStrategy.Name,
            Aliases: new[] { "Entity" }); // "Entity" is already a Name of an existing definition

        Assert.Throws<InvalidOperationException>(() => ComponentDefinitionRegistry.Register(definition));
    }

    [Fact]
    public void Register_WithReplace_CleansUpOldAliases()
    {
        var typeCode = NextCustomTypeCode();
        var original = new ComponentDefinition(
            typeCode,
            $"ReplaceAlias_{Guid.NewGuid():N}",
            $"ReplAlias_{Guid.NewGuid():N}",
            "External",
            "Original.xml",
            IdentityStrategy.Name,
            Aliases: new[] { $"OldAlias_{Guid.NewGuid():N}" });

        ComponentDefinitionRegistry.Register(original);
        var oldAlias = original.Aliases![0];
        Assert.Same(original, ComponentDefinitionRegistry.GetByName(oldAlias));

        var replacement = original with
        {
            Aliases = new[] { $"NewAlias_{Guid.NewGuid():N}" },
            FilePattern = "Replacement.xml"
        };

        ComponentDefinitionRegistry.Register(replacement, replaceExisting: true);

        Assert.Null(ComponentDefinitionRegistry.GetByName(oldAlias));
        Assert.Same(replacement, ComponentDefinitionRegistry.GetByName(replacement.Aliases![0]));
    }

    [Fact]
    public void Aliases_AreExposedOnDefinition()
    {
        var entityDef = ComponentDefinitionRegistry.GetByType(ComponentType.Entity);
        Assert.NotNull(entityDef);
        Assert.NotNull(entityDef.Aliases);
        Assert.Contains("Table", entityDef.Aliases);
    }

    private static ComponentType NextCustomTypeCode() => (ComponentType)Interlocked.Increment(ref _customTypeSeed);
}
