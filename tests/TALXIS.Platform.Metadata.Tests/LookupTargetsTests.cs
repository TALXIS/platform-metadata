using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Components.Attributes;

namespace TALXIS.Platform.Metadata.Tests;

public class LookupTargetsTests
{
    private static readonly RelationshipMetadata[] CustomerRelationships =
    {
        ManyToOne("talxis_contract", "talxis_customerid", "Account"),
        ManyToOne("talxis_contract", "talxis_customerid", "contact"),
        ManyToOne("talxis_contract", "talxis_regionid", "talxis_region"),
        ManyToOne("talxis_invoice", "talxis_customerid", "talxis_partner")
    };

    [Fact]
    public void WithoutTargetsOnTheColumn_TakesTheRelationshipsOfThatColumn()
    {
        var customer = new LookupAttributeMetadata { LogicalName = "talxis_customerid", LookupKind = LookupKind.Customer };

        Assert.Equal(new[] { "account", "contact" }, LookupTargets.Resolve(customer, "talxis_contract", CustomerRelationships));
    }

    [Fact]
    public void TargetsOnTheColumn_WinOverTheRelationships()
    {
        var customer = new LookupAttributeMetadata { LogicalName = "talxis_customerid", Targets = new[] { "Account" } };

        Assert.Equal(new[] { "account" }, LookupTargets.Resolve(customer, "talxis_contract", CustomerRelationships));
    }

    [Fact]
    public void BlankTargetsOnTheColumn_FallBackToTheRelationships()
    {
        var customer = new LookupAttributeMetadata { LogicalName = "talxis_customerid", Targets = new[] { "", " " } };

        Assert.Equal(new[] { "account", "contact" }, LookupTargets.Resolve(customer, "talxis_contract", CustomerRelationships));
    }

    [Fact]
    public void ColumnNoRelationshipBacks_HasNoTargets()
    {
        var owner = new LookupAttributeMetadata { LogicalName = "ownerid", LookupKind = LookupKind.Owner };

        Assert.Empty(LookupTargets.Resolve(owner, "talxis_contract", CustomerRelationships));
    }

    private static OneToManyRelationshipMetadata ManyToOne(string referencingEntity, string referencingAttribute, string referencedEntity) =>
        new()
        {
            SchemaName = $"{referencingEntity}_{referencingAttribute}_{referencedEntity}",
            ReferencingEntity = referencingEntity,
            ReferencingAttribute = referencingAttribute,
            ReferencedEntity = referencedEntity,
            ReferencedAttribute = $"{referencedEntity.ToLowerInvariant()}id"
        };
}
