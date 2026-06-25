using TALXIS.Platform.Metadata;
using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Components.Attributes;
using TALXIS.Platform.Metadata.Serialization.Xml;
using TALXIS.Platform.Metadata.Solutions;
using TALXIS.Platform.Metadata.Validation;

namespace TALXIS.Platform.Metadata.Tests;

public class RelationshipValidatorTests
{
    private readonly RelationshipValidator _validator = new();

    private List<ValidationResult> Errors(Workspace ws) =>
        _validator.Validate(ws).Where(r => r.Severity == ValidationSeverity.Error).ToList();

    private List<ValidationResult> Warnings(Workspace ws) =>
        _validator.Validate(ws).Where(r => r.Severity == ValidationSeverity.Warning).ToList();

    private static EntityMetadata Entity(string logicalName, params string[] lookupAttributes)
    {
        var entity = new EntityMetadata { LogicalName = logicalName };
        foreach (var attr in lookupAttributes)
            entity.AddAttribute(new LookupAttributeMetadata { LogicalName = attr, IsCustomAttribute = true });
        return entity;
    }

    private static OneToManyRelationshipMetadata Rel(string name, string referencingEntity, string referencingAttribute) =>
        new()
        {
            SchemaName = name,
            ReferencedEntity = "pba_parent",
            ReferencedAttribute = "pba_parentid",
            ReferencingEntity = referencingEntity,
            ReferencingAttribute = referencingAttribute,
        };

    // ---- Part 1: relationship -> column ----

    [Fact]
    public void ReferencingAttributeMissing_ReportsError()
    {
        var ws = new Workspace("test");
        ws.AddEntity(Entity("pba_child", "pba_lookup"));
        ws.AddRelationship(Rel("pba_parent_pba_child", "pba_child", "pba_missing"));

        var errors = Errors(ws);

        var error = Assert.Single(errors);
        Assert.Contains("pba_missing", error.Message);
        Assert.Contains("pba_child", error.Message);
    }

    [Fact]
    public void ReferencingAttributeExistsAndReferenced_NoFindings()
    {
        var ws = new Workspace("test");
        ws.AddEntity(Entity("pba_child", "pba_lookup"));
        ws.AddRelationship(Rel("pba_parent_pba_child", "pba_child", "pba_lookup"));

        Assert.Empty(_validator.Validate(ws));
    }

    [Fact]
    public void CasingDiffers_StillMatches_NoFindings()
    {
        var ws = new Workspace("test");
        ws.AddEntity(Entity("pba_child", "pba_lookup"));
        ws.AddRelationship(Rel("rel", "PBA_Child", "PBA_Lookup"));

        Assert.Empty(_validator.Validate(ws));
    }

    [Theory]
    [InlineData("ownerid")]
    [InlineData("OwnerId")]
    [InlineData("owningbusinessunit")]
    [InlineData("createdby")]
    [InlineData("modifiedonbehalfby")]
    public void SystemReferencingAttribute_NotReported(string systemAttribute)
    {
        var ws = new Workspace("test");
        ws.AddEntity(Entity("pba_child"));
        ws.AddRelationship(Rel("system_rel", "pba_child", systemAttribute));

        Assert.Empty(_validator.Validate(ws));
    }

    [Fact]
    public void ReferencingEntityNotInSolution_Skipped()
    {
        var ws = new Workspace("test");
        ws.AddRelationship(Rel("rel", "account", "pba_whatever"));

        Assert.Empty(_validator.Validate(ws));
    }

    [Fact]
    public void ManyToManyRelationship_Ignored()
    {
        var ws = new Workspace("test");
        ws.AddEntity(Entity("pba_child"));
        ws.AddRelationship(new ManyToManyRelationshipMetadata
        {
            SchemaName = "pba_a_pba_b",
            Entity1LogicalName = "pba_child",
            Entity2LogicalName = "pba_other",
            IntersectEntityName = "pba_a_pba_b",
        });

        Assert.Empty(_validator.Validate(ws));
    }

    [Fact]
    public void UnresolvedRelationship_NameOnly_ReportsError()
    {
        var ws = new Workspace("test");
        ws.AddRelationship(new OneToManyRelationshipMetadata
        {
            SchemaName = "udpp_udpp_111warehouseitem_udpp_warehousetransaction_itemid",
            ReferencingEntity = "",
            ReferencingAttribute = "",
            ReferencedEntity = "",
            ReferencedAttribute = "",
        });

        var error = Assert.Single(Errors(ws));
        Assert.Contains("udpp_udpp_111warehouseitem_udpp_warehousetransaction_itemid", error.Message);
        Assert.Contains("no definition", error.Message);
    }

    [Fact]
    public void MultipleDanglingRelationships_AllReportedAsErrors()
    {
        var ws = new Workspace("test");
        ws.AddEntity(Entity("pba_child", "pba_good"));
        ws.AddRelationship(Rel("rel_good", "pba_child", "pba_good"));
        ws.AddRelationship(Rel("rel_bad1", "pba_child", "pba_missing1"));
        ws.AddRelationship(Rel("rel_bad2", "pba_child", "pba_missing2"));

        var errors = Errors(ws);

        Assert.Equal(2, errors.Count);
        Assert.All(errors, e => Assert.Contains("missing", e.Message));
    }

    [Fact]
    public void ReferencingAttributeWrongType_ReportsWarning()
    {
        var ws = new Workspace("test");
        var entity = new EntityMetadata { LogicalName = "pba_child" };
        entity.AddAttribute(new StringAttributeMetadata { LogicalName = "pba_text" });
        ws.AddEntity(entity);
        ws.AddRelationship(Rel("rel", "pba_child", "pba_text"));

        Assert.Empty(Errors(ws));
        var warning = Assert.Single(Warnings(ws));
        Assert.Contains("pba_text", warning.Message);
        Assert.Contains("String", warning.Message);
    }

    // ---- Part 2: column -> relationship (orphan lookups) ----

    [Fact]
    public void OrphanLookupColumn_ReportsWarning()
    {
        var ws = new Workspace("test");
        ws.AddEntity(Entity("pba_child", "pba_orphan"));
        // no relationship references pba_orphan

        Assert.Empty(Errors(ws));
        var warning = Assert.Single(Warnings(ws));
        Assert.Contains("pba_orphan", warning.Message);
        Assert.Contains("another solution", warning.Message);
    }

    [Fact]
    public void NonCustomLookupColumn_NotWarned()
    {
        // A system (non-custom) lookup must not be treated as an orphan,
        // regardless of its name — detection is by the IsCustomAttribute flag,
        // so this holds for any project without a hard-coded name list.
        var ws = new Workspace("test");
        var entity = new EntityMetadata { LogicalName = "pba_child" };
        entity.AddAttribute(new LookupAttributeMetadata { LogicalName = "createdby", IsCustomAttribute = false });
        ws.AddEntity(entity);

        Assert.Empty(_validator.Validate(ws));
    }

    [Fact]
    public void LookupColumnWithRelationship_NoWarning()
    {
        var ws = new Workspace("test");
        ws.AddEntity(Entity("pba_child", "pba_lookup"));
        ws.AddRelationship(Rel("rel", "pba_child", "pba_lookup"));

        Assert.Empty(Warnings(ws));
    }

    // ---- Referenced (one) side of 1:N ----

    [Fact]
    public void ReferencedKeyColumnMissing_ReportsError()
    {
        var ws = new Workspace("test");
        ws.AddEntity(Entity("pba_child", "pba_lookup"));
        ws.AddEntity(new EntityMetadata { LogicalName = "pba_parent" }); // no pba_parentid key column
        ws.AddRelationship(Rel("rel", "pba_child", "pba_lookup")); // referenced = pba_parent / pba_parentid

        var error = Assert.Single(Errors(ws));
        Assert.Contains("pba_parentid", error.Message);
        Assert.Contains("pba_parent", error.Message);
    }

    // ---- Self-referential ----

    [Fact]
    public void SelfReferentialOneToMany_Valid_NoFindings()
    {
        var ws = new Workspace("test");
        var node = new EntityMetadata { LogicalName = "pba_node" };
        node.AddAttribute(new LookupAttributeMetadata { LogicalName = "pba_parentnode", IsCustomAttribute = true });
        node.AddAttribute(new UniqueIdentifierAttributeMetadata { LogicalName = "pba_nodeid" });
        ws.AddEntity(node);
        ws.AddRelationship(new OneToManyRelationshipMetadata
        {
            SchemaName = "pba_node_selfref",
            ReferencedEntity = "pba_node",
            ReferencedAttribute = "pba_nodeid",
            ReferencingEntity = "pba_node",
            ReferencingAttribute = "pba_parentnode",
        });

        Assert.Empty(_validator.Validate(ws));
    }

    [Fact]
    public void SelfReferentialOneToMany_MissingColumn_ReportsError()
    {
        var ws = new Workspace("test");
        var node = new EntityMetadata { LogicalName = "pba_node" };
        node.AddAttribute(new UniqueIdentifierAttributeMetadata { LogicalName = "pba_nodeid" });
        ws.AddEntity(node);
        ws.AddRelationship(new OneToManyRelationshipMetadata
        {
            SchemaName = "pba_node_selfref",
            ReferencedEntity = "pba_node",
            ReferencedAttribute = "pba_nodeid",
            ReferencingEntity = "pba_node",
            ReferencingAttribute = "pba_parentnode", // missing on the entity
        });

        var error = Assert.Single(Errors(ws));
        Assert.Contains("pba_parentnode", error.Message);
    }

    // ---- Many-to-many ----

    [Fact]
    public void ManyToManyWellFormed_NoFindings()
    {
        var ws = new Workspace("test");
        ws.AddRelationship(new ManyToManyRelationshipMetadata
        {
            SchemaName = "pba_a_pba_b",
            Entity1LogicalName = "pba_a",
            Entity2LogicalName = "pba_b",
            IntersectEntityName = "pba_a_pba_b",
        });

        Assert.Empty(_validator.Validate(ws));
    }

    [Fact]
    public void ManyToManySelfReferential_NoFindings()
    {
        var ws = new Workspace("test");
        ws.AddEntity(Entity("pba_node"));
        ws.AddRelationship(new ManyToManyRelationshipMetadata
        {
            SchemaName = "pba_node_node",
            Entity1LogicalName = "pba_node",
            Entity2LogicalName = "pba_node",
            IntersectEntityName = "pba_node_node",
        });

        Assert.Empty(_validator.Validate(ws));
    }

    [Fact]
    public void ManyToManyMissingEnd_ReportsError()
    {
        var ws = new Workspace("test");
        ws.AddRelationship(new ManyToManyRelationshipMetadata
        {
            SchemaName = "pba_broken",
            Entity1LogicalName = "pba_a",
            Entity2LogicalName = "",   // missing end
            IntersectEntityName = "pba_broken",
        });

        var error = Assert.Single(Errors(ws));
        Assert.Contains("pba_broken", error.Message);
    }

    // ---- Shell vs full entity (root-component behavior) ----

    private static void Include(Workspace ws, string entityLogicalName, int behavior)
    {
        var solution = new Solution { UniqueName = "test_solution" };
        solution.AddRootComponent(new RootComponent
        {
            Type = ComponentType.Entity,
            SchemaName = entityLogicalName,
            Behavior = behavior,
        });
        ws.AddSolution(solution);
    }

    [Fact]
    public void MissingColumn_FullEntity_ReportsError()
    {
        // behavior 0 = include subcomponents → the entity is complete here, so a
        // missing column is a real, import-breaking defect.
        var ws = new Workspace("test");
        ws.AddEntity(Entity("pba_child"));
        Include(ws, "pba_child", behavior: 0);
        ws.AddRelationship(Rel("rel", "pba_child", "pba_missing"));

        var error = Assert.Single(Errors(ws));
        Assert.Contains("pba_missing", error.Message);
        Assert.Empty(Warnings(ws));
    }

    [Fact]
    public void MissingColumn_ShellEntity_ReportsWarning()
    {
        // behavior 2 = include as shell only → the entity is partial here, so the
        // column likely lives in another solution: warning, not error.
        var ws = new Workspace("test");
        ws.AddEntity(Entity("pba_child"));
        Include(ws, "pba_child", behavior: 2);
        ws.AddRelationship(Rel("rel", "pba_child", "pba_missing"));

        Assert.Empty(Errors(ws));
        var warning = Assert.Single(Warnings(ws));
        Assert.Contains("pba_missing", warning.Message);
        Assert.Contains("shell", warning.Message);
    }

    [Fact]
    public void MissingColumn_DoNotIncludeSubcomponents_ReportsWarning()
    {
        // behavior 1 = do not include subcomponents → also a shell.
        var ws = new Workspace("test");
        ws.AddEntity(Entity("pba_child"));
        Include(ws, "pba_child", behavior: 1);
        ws.AddRelationship(Rel("rel", "pba_child", "pba_missing"));

        Assert.Empty(Errors(ws));
        Assert.Single(Warnings(ws));
    }

    [Fact]
    public void OrphanLookupColumn_OnShellEntity_StillWarned()
    {
        // Even on a shell entity the missing relationship is a real fact we cannot
        // confirm is resolved elsewhere, so it is surfaced as a warning — not
        // silently swallowed.
        var ws = new Workspace("test");
        ws.AddEntity(Entity("pba_child", "pba_orphan"));
        Include(ws, "pba_child", behavior: 2);

        Assert.Empty(Errors(ws));
        var warning = Assert.Single(Warnings(ws));
        Assert.Contains("pba_orphan", warning.Message);
    }
}
