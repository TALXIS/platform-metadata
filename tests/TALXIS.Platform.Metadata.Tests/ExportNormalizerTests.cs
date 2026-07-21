using System.Xml.Linq;
using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Serialization.Xml;
using TALXIS.Platform.Metadata.Solutions;

namespace TALXIS.Platform.Metadata.Tests;

public class ExportNormalizerTests
{
    [Fact]
    public void StripsServerAddedSystemRelationships()
    {
        var exported = CreateWorkspaceWithSolution("TestSolution");
        exported.AddRelationship(OneToMany("owner_tp_project", "owner", "tp_project"));
        exported.AddRelationship(OneToMany("business_unit_tp_project", "businessunit", "tp_project"));
        exported.AddRelationship(OneToMany("tp_project_tp_task", "tp_project", "tp_task"));

        var source = CreateWorkspaceWithSolution("TestSolution");

        var result = new ExportNormalizer().Normalize(exported, source, OnlyRule(o => o.StripSystemRelationships = true));

        Assert.Single(exported.Relationships);
        Assert.Equal("tp_project_tp_task", exported.Relationships[0].SchemaName);
        Assert.Equal(2, result.Changes.Count);
        Assert.All(result.Changes, c => Assert.Equal(ExportNormalizationRule.SystemRelationship, c.Rule));
    }

    [Fact]
    public void StripsSystemRelationshipsByNamePatternWhenParticipantsAreUnknown()
    {
        var exported = CreateWorkspaceWithSolution("TestSolution");
        exported.AddRelationship(OneToMany("lk_tp_project_createdby", "", ""));
        exported.AddRelationship(new ManyToManyRelationshipMetadata
        {
            SchemaName = "team_tp_project",
            Entity1LogicalName = "team",
            Entity2LogicalName = "tp_project",
            IntersectEntityName = "team_tp_project"
        });
        exported.AddRelationship(OneToMany("tp_project_tp_task", "", ""));

        var source = CreateWorkspaceWithSolution("TestSolution");

        var result = new ExportNormalizer().Normalize(exported, source, OnlyRule(o => o.StripSystemRelationships = true));

        Assert.Single(exported.Relationships);
        Assert.Equal("tp_project_tp_task", exported.Relationships[0].SchemaName);
        Assert.Equal(2, result.Changes.Count);
    }

    [Fact]
    public void KeepsSystemRelationshipPresentInSource()
    {
        var exported = CreateWorkspaceWithSolution("TestSolution");
        exported.AddRelationship(OneToMany("owner_tp_project", "owner", "tp_project"));

        var source = CreateWorkspaceWithSolution("TestSolution");
        source.AddRelationship(OneToMany("owner_tp_project", "owner", "tp_project"));

        var result = new ExportNormalizer().Normalize(exported, source, OnlyRule(o => o.StripSystemRelationships = true));

        Assert.Single(exported.Relationships);
        Assert.False(result.HasChanges);
    }

    [Fact]
    public void StripsComponentsNotInSourceRootComponents()
    {
        var exported = CreateWorkspaceWithSolution("TestSolution");
        exported.AddEntity(new EntityMetadata { LogicalName = "tp_keep" });
        exported.AddEntity(new EntityMetadata { LogicalName = "tp_leak" });
        exported.AddForm(new FormMetadata { FormId = "{11111111-0000-0000-0000-000000000001}", EntityLogicalName = "tp_leak" });
        exported.AddRelationship(OneToMany("tp_leak_tp_keep", "tp_keep", "tp_leak"));

        var source = CreateWorkspaceWithSolution("TestSolution", solution =>
            solution.AddRootComponent(new RootComponent { Type = ComponentType.Entity, SchemaName = "tp_keep" }));

        var result = new ExportNormalizer().Normalize(exported, source, OnlyRule(o => o.StripComponentsNotInSource = true));

        Assert.Single(exported.Entities);
        Assert.Equal("tp_keep", exported.Entities[0].LogicalName);
        Assert.Empty(exported.Forms);
        Assert.Empty(exported.Relationships);
        Assert.Contains(result.Changes, c => c.Rule == ExportNormalizationRule.ComponentNotInSource && c.Target == "tp_leak");
        Assert.Contains(result.Changes, c => c.Rule == ExportNormalizationRule.ComponentNotInSource && c.Target == "tp_leak_tp_keep");
    }

    [Fact]
    public void SkipsComponentStripWhenSourceDeclaresNoRootComponents()
    {
        var exported = CreateWorkspaceWithSolution("TestSolution");
        exported.AddEntity(new EntityMetadata { LogicalName = "tp_fresh" });
        exported.AddForm(new FormMetadata { FormId = "{33333333-0000-0000-0000-000000000001}", EntityLogicalName = "tp_fresh" });

        var source = CreateWorkspaceWithSolution("TestSolution");

        var result = new ExportNormalizer().Normalize(exported, source, OnlyRule(o => o.StripComponentsNotInSource = true));

        Assert.Single(exported.Entities);
        Assert.Single(exported.Forms);
        Assert.False(result.HasChanges);
    }

    [Fact]
    public void RemovesRootComponentEntryOfStrippedComponent()
    {
        var exported = CreateWorkspaceWithSolution("TestSolution", solution =>
        {
            solution.AddRootComponent(new RootComponent { Type = ComponentType.Entity, SchemaName = "tp_keep" });
            solution.AddRootComponent(new RootComponent { Type = ComponentType.Entity, SchemaName = "tp_leak" });
        });
        exported.AddEntity(new EntityMetadata { LogicalName = "tp_keep" });
        exported.AddEntity(new EntityMetadata { LogicalName = "tp_leak" });

        var source = CreateWorkspaceWithSolution("TestSolution", solution =>
            solution.AddRootComponent(new RootComponent { Type = ComponentType.Entity, SchemaName = "tp_keep" }));

        new ExportNormalizer().Normalize(exported, source, OnlyRule(o => o.StripComponentsNotInSource = true));

        var remaining = Assert.Single(exported.Solutions[0].RootComponents);
        Assert.Equal("tp_keep", remaining.SchemaName);
    }

    [Fact]
    public void StripsSubcomponentsOfEntityThatExcludesThem()
    {
        var declaredFormId = Guid.NewGuid();
        var exported = CreateWorkspaceWithSolution("TestSolution");
        exported.AddEntity(new EntityMetadata { LogicalName = "tp_shell" });
        exported.AddEntity(new EntityMetadata { LogicalName = "tp_full" });
        exported.AddForm(new FormMetadata { FormId = "{44444444-0000-0000-0000-000000000001}", EntityLogicalName = "tp_shell" });
        exported.AddForm(new FormMetadata { FormId = $"{{{declaredFormId}}}", EntityLogicalName = "tp_shell" });
        exported.AddView(new SavedQueryMetadata { SavedQueryId = "{44444444-0000-0000-0000-000000000002}", EntityLogicalName = "tp_shell" });
        exported.AddForm(new FormMetadata { FormId = "{44444444-0000-0000-0000-000000000003}", EntityLogicalName = "tp_full" });

        var source = CreateWorkspaceWithSolution("TestSolution", solution =>
        {
            solution.AddRootComponent(new RootComponent { Type = ComponentType.Entity, SchemaName = "tp_shell", Behavior = 2 });
            solution.AddRootComponent(new RootComponent { Type = ComponentType.Entity, SchemaName = "tp_full", Behavior = 0 });
            solution.AddRootComponent(new RootComponent { Type = ComponentType.SystemForm, Id = declaredFormId });
        });

        var result = new ExportNormalizer().Normalize(exported, source, OnlyRule(o => o.EnforceRootComponentBehavior = true));

        Assert.Equal(2, exported.Entities.Count);
        Assert.Equal(2, exported.Forms.Count);
        Assert.Contains(exported.Forms, f => IdEquals(f.FormId, declaredFormId));
        Assert.Empty(exported.Views);
        Assert.Equal(2, result.Changes.Count);
        Assert.All(result.Changes, c => Assert.Equal(ExportNormalizationRule.ExcludedSubcomponent, c.Rule));

        static bool IdEquals(string value, Guid id) => Guid.TryParse(value, out var parsed) && parsed == id;
    }

    [Fact]
    public void KeepsAlreadyPulledSubcomponentsWhenBehaviorChanges()
    {
        var exported = CreateWorkspaceWithSolution("TestSolution");
        exported.AddEntity(new EntityMetadata { LogicalName = "tp_shell" });
        exported.AddForm(new FormMetadata { FormId = "{55555555-0000-0000-0000-000000000001}", EntityLogicalName = "tp_shell" });
        exported.AddForm(new FormMetadata { FormId = "{55555555-0000-0000-0000-000000000002}", EntityLogicalName = "tp_shell" });

        var source = CreateWorkspaceWithSolution("TestSolution", solution =>
            solution.AddRootComponent(new RootComponent { Type = ComponentType.Entity, SchemaName = "tp_shell", Behavior = 2 }));
        source.AddForm(new FormMetadata { FormId = "{55555555-0000-0000-0000-000000000001}", EntityLogicalName = "tp_shell" });

        var result = new ExportNormalizer().Normalize(exported, source, OnlyRule(o => o.EnforceRootComponentBehavior = true));

        var kept = Assert.Single(exported.Forms);
        Assert.Equal("{55555555-0000-0000-0000-000000000001}", kept.FormId);
        var change = Assert.Single(result.Changes);
        Assert.Equal("{55555555-0000-0000-0000-000000000002}", change.Target);
    }

    [Fact]
    public void KeepsComponentPresentInSourceEvenWithoutRootComponent()
    {
        var exported = CreateWorkspaceWithSolution("TestSolution");
        exported.AddEntity(new EntityMetadata { LogicalName = "tp_extra" });

        var source = CreateWorkspaceWithSolution("TestSolution");
        source.AddEntity(new EntityMetadata { LogicalName = "tp_extra" });

        var result = new ExportNormalizer().Normalize(exported, source, OnlyRule(o => o.StripComponentsNotInSource = true));

        Assert.Single(exported.Entities);
        Assert.False(result.HasChanges);
    }

    [Fact]
    public void MatchesRootComponentById()
    {
        var workflowId = Guid.NewGuid();
        var exported = CreateWorkspaceWithSolution("TestSolution");
        exported.AddWorkflow(new WorkflowMetadata { WorkflowId = $"{{{workflowId}}}" });

        var source = CreateWorkspaceWithSolution("TestSolution", solution =>
            solution.AddRootComponent(new RootComponent { Type = ComponentType.Workflow, Id = workflowId }));

        var result = new ExportNormalizer().Normalize(exported, source, OnlyRule(o => o.StripComponentsNotInSource = true));

        Assert.Single(exported.Workflows);
        Assert.False(result.HasChanges);
    }

    [Fact]
    public void StripsFormOwnedByAnotherSourceSolution()
    {
        const string leakedFormId = "{22222222-0000-0000-0000-000000000001}";
        const string ownFormId = "{22222222-0000-0000-0000-000000000002}";

        var exported = CreateWorkspaceWithSolution("TestSolution");
        exported.AddForm(new FormMetadata { FormId = leakedFormId, EntityLogicalName = "tp_project" });
        exported.AddForm(new FormMetadata { FormId = ownFormId, EntityLogicalName = "tp_project" });

        var source = new Workspace("source");
        var currentSolution = NewSolution("TestSolution");
        var otherSolution = NewSolution("OtherSolution");
        source.AddSolution(currentSolution);
        source.AddSolution(otherSolution);

        var ownForm = new FormMetadata { FormId = ownFormId, EntityLogicalName = "tp_project" };
        var leakedForm = new FormMetadata { FormId = leakedFormId, EntityLogicalName = "tp_project" };
        source.RegisterSolutionSource(currentSolution, 0, null, new[]
        {
            new LayerComponentDescriptor(ComponentType.SystemForm, ownFormId, ownForm, $"Form:tp_project:{ownFormId}")
        });
        source.RegisterSolutionSource(otherSolution, 1, null, new[]
        {
            new LayerComponentDescriptor(ComponentType.SystemForm, leakedFormId, leakedForm, $"Form:tp_project:{leakedFormId}")
        });

        var result = new ExportNormalizer().Normalize(exported, source, OnlyRule(o => o.StripComponentsOwnedByOtherSolutions = true));

        Assert.Single(exported.Forms);
        Assert.Equal(ownFormId, exported.Forms[0].FormId);
        var change = Assert.Single(result.Changes);
        Assert.Equal(ExportNormalizationRule.CrossSolutionComponent, change.Rule);
        Assert.Contains("OtherSolution", change.Description);
    }

    [Fact]
    public void NormalizesManagedFlagAndVersionToSource()
    {
        var exported = CreateWorkspaceWithSolution("TestSolution", solution =>
        {
            solution.ManagedValue = "1";
            solution.Version = "1.0.0.5";
        });
        var source = CreateWorkspaceWithSolution("TestSolution", solution =>
        {
            solution.ManagedValue = "0";
            solution.Version = "1.0.0.0";
        });

        var result = new ExportNormalizer().Normalize(exported, source);

        Assert.Equal("0", exported.Solutions[0].ManagedValue);
        Assert.Equal("1.0.0.0", exported.Solutions[0].Version);
        Assert.Contains(result.Changes, c => c.Rule == ExportNormalizationRule.ManagedFlag);
        Assert.Contains(result.Changes, c => c.Rule == ExportNormalizationRule.SolutionVersion);
    }

    [Fact]
    public void ThrowsWhenSourceSolutionIsMissing()
    {
        var exported = CreateWorkspaceWithSolution("TestSolution");
        var source = CreateWorkspaceWithSolution("DifferentSolution");

        Assert.Throws<InvalidOperationException>(() => new ExportNormalizer().Normalize(exported, source));
    }

    [Fact]
    public void StripsServerVersionAttributesFromWrittenFiles()
    {
        var exportedDir = Path.Combine(Path.GetTempPath(), $"normalizer-exported-{Guid.NewGuid():N}");
        var sourceDir = Path.Combine(Path.GetTempPath(), $"normalizer-source-{Guid.NewGuid():N}");
        var outputDir = Path.Combine(Path.GetTempPath(), $"normalizer-output-{Guid.NewGuid():N}");
        try
        {
            WriteSolutionProject(exportedDir, managed: "1", version: "1.0.0.7", serverAttributes: true);
            WriteSolutionProject(sourceDir, managed: "0", version: "1.0.0.0", serverAttributes: false);

            var reader = new XmlWorkspaceReader();
            var exported = reader.Load(exportedDir);
            var source = reader.Load(sourceDir);

            var result = new ExportNormalizer().Normalize(exported, source);

            new XmlWorkspaceWriter().Write(exported, outputDir);

            var solutionRoot = XDocument.Load(Path.Combine(outputDir, "Other", "Solution.xml")).Root!;
            Assert.Null(solutionRoot.Attribute("OrganizationVersion"));
            Assert.Null(solutionRoot.Attribute("OrganizationSchemaType"));
            Assert.Null(solutionRoot.Attribute("CRMServerServiceabilityVersion"));

            var customizationsRoot = XDocument.Load(Path.Combine(outputDir, "Other", "Customizations.xml")).Root!;
            Assert.Null(customizationsRoot.Attribute("OrganizationVersion"));
            Assert.Null(customizationsRoot.Attribute("OrganizationSchemaType"));
            Assert.Null(customizationsRoot.Attribute("CRMServerServiceabilityVersion"));

            var manifest = solutionRoot.Element("SolutionManifest")!;
            Assert.Equal("0", manifest.Element("Managed")!.Value);
            Assert.Equal("1.0.0.0", manifest.Element("Version")!.Value);

            Assert.Equal(6, result.Changes.Count(c => c.Rule == ExportNormalizationRule.ServerVersionAttribute));
        }
        finally
        {
            foreach (var dir in new[] { exportedDir, sourceDir, outputDir })
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }

    [Fact]
    public void DeletesRelationshipFilesWhenAllRelationshipsAreStripped()
    {
        var exportedDir = Path.Combine(Path.GetTempPath(), $"normalizer-exported-{Guid.NewGuid():N}");
        var sourceDir = Path.Combine(Path.GetTempPath(), $"normalizer-source-{Guid.NewGuid():N}");
        try
        {
            WriteSolutionProject(exportedDir, managed: "0", version: "1.0.0.0", serverAttributes: false);
            WriteSolutionProject(sourceDir, managed: "0", version: "1.0.0.0", serverAttributes: false);
            File.WriteAllText(Path.Combine(exportedDir, "Other", "Relationships.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <EntityRelationships xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
                  <EntityRelationship Name="owner_tp_project" />
                  <EntityRelationship Name="business_unit_tp_project" />
                </EntityRelationships>
                """);
            Directory.CreateDirectory(Path.Combine(exportedDir, "Other", "Relationships"));
            File.WriteAllText(Path.Combine(exportedDir, "Other", "Relationships", "Owner.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <EntityRelationships xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
                  <EntityRelationship Name="owner_tp_project" />
                </EntityRelationships>
                """);

            var reader = new XmlWorkspaceReader();
            var exported = reader.Load(exportedDir);
            var source = reader.Load(sourceDir);

            new ExportNormalizer().Normalize(exported, source);
            Assert.Empty(exported.Relationships);

            new XmlWorkspaceWriter().Write(exported, exportedDir);

            Assert.False(File.Exists(Path.Combine(exportedDir, "Other", "Relationships.xml")));
            Assert.False(File.Exists(Path.Combine(exportedDir, "Other", "Relationships", "Owner.xml")));
            Assert.False(Directory.Exists(Path.Combine(exportedDir, "Other", "Relationships")));
        }
        finally
        {
            foreach (var dir in new[] { exportedDir, sourceDir })
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }

    [Fact]
    public void KeepsRelationshipsFileFormattingAfterStrip()
    {
        var exportedDir = Path.Combine(Path.GetTempPath(), $"normalizer-exported-{Guid.NewGuid():N}");
        var sourceDir = Path.Combine(Path.GetTempPath(), $"normalizer-source-{Guid.NewGuid():N}");
        try
        {
            WriteSolutionProject(exportedDir, managed: "0", version: "1.0.0.0", serverAttributes: false);
            WriteSolutionProject(sourceDir, managed: "0", version: "1.0.0.0", serverAttributes: false);
            File.WriteAllText(Path.Combine(exportedDir, "Other", "Relationships.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <EntityRelationships xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
                  <EntityRelationship Name="owner_tp_project" />
                  <EntityRelationship Name="business_unit_tp_project" />
                  <EntityRelationship Name="tp_project_account" />
                </EntityRelationships>
                """);

            var reader = new XmlWorkspaceReader();
            var exported = reader.Load(exportedDir);
            var source = reader.Load(sourceDir);

            new ExportNormalizer().Normalize(exported, source);
            new XmlWorkspaceWriter().Write(exported, exportedDir);

            var written = File.ReadAllText(Path.Combine(exportedDir, "Other", "Relationships.xml"));
            Assert.Contains("<EntityRelationship Name=\"tp_project_account\" />", written);
            Assert.DoesNotContain("EntityRelationshipType", written);
            Assert.DoesNotContain("owner_tp_project", written);

            var lines = written.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
            Assert.DoesNotContain(lines, line => line.Length > 0 && line.Trim().Length == 0);
        }
        finally
        {
            foreach (var dir in new[] { exportedDir, sourceDir })
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }

    [Fact]
    public void KeepsRelationshipDefinitionInSinglePerEntityFile()
    {
        var exportedDir = Path.Combine(Path.GetTempPath(), $"normalizer-exported-{Guid.NewGuid():N}");
        var sourceDir = Path.Combine(Path.GetTempPath(), $"normalizer-source-{Guid.NewGuid():N}");
        try
        {
            WriteSolutionProject(exportedDir, managed: "0", version: "1.0.0.0", serverAttributes: false);
            WriteSolutionProject(sourceDir, managed: "0", version: "1.0.0.0", serverAttributes: false);
            File.WriteAllText(Path.Combine(exportedDir, "Other", "Relationships.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <EntityRelationships xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
                  <EntityRelationship Name="tp_project_account" />
                </EntityRelationships>
                """);
            Directory.CreateDirectory(Path.Combine(exportedDir, "Other", "Relationships"));
            File.WriteAllText(Path.Combine(exportedDir, "Other", "Relationships", "Account.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <EntityRelationships xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
                  <EntityRelationship Name="tp_project_account">
                    <EntityRelationshipType>OneToMany</EntityRelationshipType>
                    <ReferencingEntityName>tp_project</ReferencingEntityName>
                    <ReferencedEntityName>Account</ReferencedEntityName>
                  </EntityRelationship>
                </EntityRelationships>
                """);
            File.WriteAllText(Path.Combine(exportedDir, "Other", "Relationships", "Owner.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <EntityRelationships xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
                  <EntityRelationship Name="owner_tp_project">
                    <EntityRelationshipType>OneToMany</EntityRelationshipType>
                    <ReferencingEntityName>tp_project</ReferencingEntityName>
                    <ReferencedEntityName>Owner</ReferencedEntityName>
                  </EntityRelationship>
                </EntityRelationships>
                """);

            var reader = new XmlWorkspaceReader();
            var exported = reader.Load(exportedDir);
            var source = reader.Load(sourceDir);

            new ExportNormalizer().Normalize(exported, source);
            new XmlWorkspaceWriter().Write(exported, exportedDir);

            var mainText = File.ReadAllText(Path.Combine(exportedDir, "Other", "Relationships.xml"));
            Assert.Contains("<EntityRelationship Name=\"tp_project_account\" />", mainText);
            Assert.DoesNotContain("ReferencedEntityName", mainText);

            var accountDoc = XDocument.Load(Path.Combine(exportedDir, "Other", "Relationships", "Account.xml"));
            var definition = Assert.Single(accountDoc.Root!.Elements("EntityRelationship"));
            Assert.NotNull(definition.Element("ReferencedEntityName"));

            Assert.False(File.Exists(Path.Combine(exportedDir, "Other", "Relationships", "tp_project.xml")));
            Assert.False(File.Exists(Path.Combine(exportedDir, "Other", "Relationships", "Owner.xml")));
        }
        finally
        {
            foreach (var dir in new[] { exportedDir, sourceDir })
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }

    [Fact]
    public void StripsServerOwnedAttributesMissingFromSource()
    {
        var exportedDir = Path.Combine(Path.GetTempPath(), $"normalizer-exported-{Guid.NewGuid():N}");
        var sourceDir = Path.Combine(Path.GetTempPath(), $"normalizer-source-{Guid.NewGuid():N}");
        try
        {
            WriteSolutionProject(exportedDir, managed: "0", version: "1.0.0.0", serverAttributes: false);
            WriteSolutionProject(sourceDir, managed: "0", version: "1.0.0.0", serverAttributes: false);
            WriteEntityWithAttributes(exportedDir, "tp_min",
                ("CreatedBy", "createdby", false),
                ("CreatedOn", "createdon", false),
                ("tp_alpha", "tp_alpha", true),
                ("tp_new", "tp_new", true));
            WriteEntityWithAttributes(sourceDir, "tp_min",
                ("CreatedOn", "createdon", false),
                ("tp_alpha", "tp_alpha", true));

            var reader = new XmlWorkspaceReader();
            var exported = reader.Load(exportedDir);
            var source = reader.Load(sourceDir);

            var result = new ExportNormalizer().Normalize(exported, source, OnlyRule(o => o.StripServerOwnedAttributes = true));

            var change = Assert.Single(result.Changes);
            Assert.Equal(ExportNormalizationRule.ServerOwnedAttribute, change.Rule);
            Assert.Equal("tp_min.createdby", change.Target);
            Assert.Equal(ComponentType.Attribute, change.ComponentType);
            Assert.Null(exported.FindEntity("tp_min")!.FindAttribute("createdby"));

            new XmlWorkspaceWriter().Write(exported, exportedDir);

            var written = File.ReadAllText(Path.Combine(exportedDir, "Entities", "tp_min", "Entity.xml"));
            Assert.DoesNotContain("CreatedBy", written);
            Assert.Contains("CreatedOn", written);
            Assert.Contains("tp_alpha", written);
            Assert.Contains("tp_new", written);
            var lines = written.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
            Assert.DoesNotContain(lines, line => line.Length > 0 && line.Trim().Length == 0);
        }
        finally
        {
            foreach (var dir in new[] { exportedDir, sourceDir })
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }

    [Fact]
    public void KeepsAllAttributesWhenEntityIsNotInSource()
    {
        var exportedDir = Path.Combine(Path.GetTempPath(), $"normalizer-exported-{Guid.NewGuid():N}");
        var sourceDir = Path.Combine(Path.GetTempPath(), $"normalizer-source-{Guid.NewGuid():N}");
        try
        {
            WriteSolutionProject(exportedDir, managed: "0", version: "1.0.0.0", serverAttributes: false);
            WriteSolutionProject(sourceDir, managed: "0", version: "1.0.0.0", serverAttributes: false);
            WriteEntityWithAttributes(exportedDir, "tp_fresh",
                ("CreatedBy", "createdby", false),
                ("tp_alpha", "tp_alpha", true));

            var reader = new XmlWorkspaceReader();
            var exported = reader.Load(exportedDir);
            var source = reader.Load(sourceDir);

            var result = new ExportNormalizer().Normalize(exported, source, OnlyRule(o => o.StripServerOwnedAttributes = true));

            Assert.False(result.HasChanges);
            Assert.NotNull(exported.FindEntity("tp_fresh")!.FindAttribute("createdby"));
        }
        finally
        {
            foreach (var dir in new[] { exportedDir, sourceDir })
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }

    private static void WriteEntityWithAttributes(string projectDir, string logicalName, params (string PhysicalName, string LogicalName, bool IsCustom)[] attributes)
    {
        var entityDir = Path.Combine(projectDir, "Entities", logicalName);
        Directory.CreateDirectory(entityDir);

        var attributeXml = string.Join("\n", attributes.Select(a =>
            $"""
                    <attribute PhysicalName="{a.PhysicalName}">
                      <Type>nvarchar</Type>
                      <Name>{a.PhysicalName}</Name>
                      <LogicalName>{a.LogicalName}</LogicalName>
                      <RequiredLevel>none</RequiredLevel>
                      <IsCustomField>{(a.IsCustom ? 1 : 0)}</IsCustomField>
                      <displaynames>
                        <displayname description="{a.PhysicalName}" languagecode="1033" />
                      </displaynames>
                    </attribute>
            """));

        File.WriteAllText(Path.Combine(entityDir, "Entity.xml"),
            $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Entity xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <EntityInfo>
                <entity Name="{logicalName}">
                  <LocalizedNames>
                    <LocalizedName description="{logicalName}" languagecode="1033" />
                  </LocalizedNames>
                  <attributes>
            {attributeXml}
                  </attributes>
                </entity>
              </EntityInfo>
            </Entity>
            """);
    }

    [Fact]
    public void DisabledRulesDoNothing()
    {
        var exported = CreateWorkspaceWithSolution("TestSolution", solution => solution.ManagedValue = "1");
        exported.AddRelationship(OneToMany("owner_tp_project", "owner", "tp_project"));
        exported.AddEntity(new EntityMetadata { LogicalName = "tp_leak" });

        var source = CreateWorkspaceWithSolution("TestSolution");

        var options = new ExportNormalizationOptions
        {
            StripSystemRelationships = false,
            StripComponentsNotInSource = false,
            EnforceRootComponentBehavior = false,
            StripServerOwnedAttributes = false,
            StripComponentsOwnedByOtherSolutions = false,
            StripServerVersionAttributes = false,
            NormalizeManagedFlag = false,
            NormalizeSolutionVersion = false
        };

        var result = new ExportNormalizer().Normalize(exported, source, options);

        Assert.False(result.HasChanges);
        Assert.Single(exported.Relationships);
        Assert.Single(exported.Entities);
        Assert.Equal("1", exported.Solutions[0].ManagedValue);
    }

    private static ExportNormalizationOptions OnlyRule(Action<ExportNormalizationOptions> enable)
    {
        var options = new ExportNormalizationOptions
        {
            StripSystemRelationships = false,
            StripComponentsNotInSource = false,
            EnforceRootComponentBehavior = false,
            StripServerOwnedAttributes = false,
            StripComponentsOwnedByOtherSolutions = false,
            StripServerVersionAttributes = false,
            NormalizeManagedFlag = false,
            NormalizeSolutionVersion = false
        };
        enable(options);
        return options;
    }

    private static Workspace CreateWorkspaceWithSolution(string uniqueName, Action<Solution>? configure = null)
    {
        var workspace = new Workspace(uniqueName);
        var solution = NewSolution(uniqueName);
        configure?.Invoke(solution);
        workspace.AddSolution(solution);
        return workspace;
    }

    private static Solution NewSolution(string uniqueName) => new() { UniqueName = uniqueName };

    private static OneToManyRelationshipMetadata OneToMany(string schemaName, string referencedEntity, string referencingEntity) => new()
    {
        SchemaName = schemaName,
        ReferencedEntity = referencedEntity,
        ReferencedAttribute = $"{referencedEntity}id",
        ReferencingEntity = referencingEntity,
        ReferencingAttribute = $"{referencedEntity}id"
    };

    private static void WriteSolutionProject(string dir, string managed, string version, bool serverAttributes)
    {
        Directory.CreateDirectory(Path.Combine(dir, "Other"));

        var attributes = serverAttributes
            ? " OrganizationVersion=\"9.2.25092.135\" OrganizationSchemaType=\"Standard\" CRMServerServiceabilityVersion=\"9.2.25092.00139\""
            : string.Empty;

        File.WriteAllText(Path.Combine(dir, "Other", "Solution.xml"),
            $"""
            <?xml version="1.0" encoding="utf-8"?>
            <ImportExportXml xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"{attributes}>
              <SolutionManifest>
                <UniqueName>TestSolution</UniqueName>
                <LocalizedNames>
                  <LocalizedName description="Test Solution" languagecode="1033" />
                </LocalizedNames>
                <Descriptions />
                <Version>{version}</Version>
                <Managed>{managed}</Managed>
                <Publisher>
                  <UniqueName>TestPub</UniqueName>
                  <LocalizedNames>
                    <LocalizedName description="Test Publisher" languagecode="1033" />
                  </LocalizedNames>
                  <Descriptions />
                  <CustomizationPrefix>tp</CustomizationPrefix>
                  <CustomizationOptionValuePrefix>10000</CustomizationOptionValuePrefix>
                </Publisher>
                <RootComponents />
                <MissingDependencies />
              </SolutionManifest>
            </ImportExportXml>
            """);

        File.WriteAllText(Path.Combine(dir, "Other", "Customizations.xml"),
            $"""
            <?xml version="1.0" encoding="utf-8"?>
            <ImportExportXml xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"{attributes}>
              <Entities />
              <Roles />
              <Workflows />
              <Languages>
                <Language>1033</Language>
              </Languages>
            </ImportExportXml>
            """);
    }
}
