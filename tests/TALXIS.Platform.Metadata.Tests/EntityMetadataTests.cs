using TALXIS.Platform.Metadata;
using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Components.Attributes;

namespace TALXIS.Platform.Metadata.Tests;

public class EntityMetadataTests
{
    [Fact]
    public void Create_WithRequiredProperties_SetsValues()
    {
        var entity = new EntityMetadata { LogicalName = "account" };

        Assert.Equal("account", entity.LogicalName);
        Assert.NotNull(entity.DisplayName);
        Assert.NotNull(entity.PluralName);
        Assert.NotNull(entity.Description);
        Assert.Empty(entity.Attributes);
        Assert.Empty(entity.Relationships);
    }

    [Fact]
    public void Create_WithAllProperties_SetsValues()
    {
        var entity = new EntityMetadata
        {
            LogicalName = "contact",
            SchemaName = "Contact",
            PrimaryIdAttribute = "contactid",
            PrimaryNameAttribute = "fullname",
            EntitySetName = "contacts",
            IsCustomEntity = true,
            IsActivity = false,
            HasNotes = true,
            HasActivities = true,
            IsAuditEnabled = true,
            ChangeTrackingEnabled = true,
            Ownership = OwnershipType.UserOwned
        };

        Assert.Equal("contact", entity.LogicalName);
        Assert.Equal("Contact", entity.SchemaName);
        Assert.Equal("contactid", entity.PrimaryIdAttribute);
        Assert.Equal("fullname", entity.PrimaryNameAttribute);
        Assert.Equal("contacts", entity.EntitySetName);
        Assert.True(entity.IsCustomEntity);
        Assert.False(entity.IsActivity);
        Assert.True(entity.HasNotes);
        Assert.True(entity.HasActivities);
        Assert.True(entity.IsAuditEnabled);
        Assert.True(entity.ChangeTrackingEnabled);
        Assert.Equal(OwnershipType.UserOwned, entity.Ownership);
    }

    [Fact]
    public void AddAttribute_AddsToCollection()
    {
        var entity = new EntityMetadata { LogicalName = "account" };
        var attr = new StringAttributeMetadata { LogicalName = "name" };

        entity.AddAttribute(attr);

        Assert.Single(entity.Attributes);
        Assert.Same(attr, entity.Attributes[0]);
    }

    [Fact]
    public void AddAttribute_MultipleAttributes_AllPresent()
    {
        var entity = new EntityMetadata { LogicalName = "account" };
        entity.AddAttribute(new StringAttributeMetadata { LogicalName = "name" });
        entity.AddAttribute(new IntegerAttributeMetadata { LogicalName = "revenue" });
        entity.AddAttribute(new BooleanAttributeMetadata { LogicalName = "active" });

        Assert.Equal(3, entity.Attributes.Count);
    }

    [Fact]
    public void FindAttribute_ReturnsCorrectAttribute()
    {
        var entity = new EntityMetadata { LogicalName = "account" };
        var nameAttr = new StringAttributeMetadata { LogicalName = "name" };
        var revenueAttr = new IntegerAttributeMetadata { LogicalName = "revenue" };
        entity.AddAttribute(nameAttr);
        entity.AddAttribute(revenueAttr);

        var found = entity.FindAttribute("revenue");

        Assert.Same(revenueAttr, found);
    }

    [Fact]
    public void FindAttribute_IsCaseInsensitive()
    {
        var entity = new EntityMetadata { LogicalName = "account" };
        var attr = new StringAttributeMetadata { LogicalName = "AccountName" };
        entity.AddAttribute(attr);

        Assert.Same(attr, entity.FindAttribute("accountname"));
        Assert.Same(attr, entity.FindAttribute("ACCOUNTNAME"));
        Assert.Same(attr, entity.FindAttribute("AccountName"));
    }

    [Fact]
    public void FindAttribute_UnknownAttribute_ReturnsNull()
    {
        var entity = new EntityMetadata { LogicalName = "account" };
        entity.AddAttribute(new StringAttributeMetadata { LogicalName = "name" });

        Assert.Null(entity.FindAttribute("nonexistent"));
    }

    [Fact]
    public void AddRelationship_AddsToCollection()
    {
        var entity = new EntityMetadata { LogicalName = "account" };
        var rel = new OneToManyRelationshipMetadata
        {
            SchemaName = "account_contacts",
            ReferencedEntity = "account",
            ReferencedAttribute = "accountid",
            ReferencingEntity = "contact",
            ReferencingAttribute = "parentcustomerid"
        };

        entity.AddRelationship(rel);

        Assert.Single(entity.Relationships);
        Assert.Same(rel, entity.Relationships[0]);
    }

    [Fact]
    public void AddRelationship_ManyToMany_AddsToCollection()
    {
        var entity = new EntityMetadata { LogicalName = "account" };
        var rel = new ManyToManyRelationshipMetadata
        {
            SchemaName = "account_leads",
            Entity1LogicalName = "account",
            Entity2LogicalName = "lead",
            IntersectEntityName = "accountleads"
        };

        entity.AddRelationship(rel);

        Assert.Single(entity.Relationships);
        Assert.IsType<ManyToManyRelationshipMetadata>(entity.Relationships[0]);
    }

    [Fact]
    public void SourceLocation_CanBeSetAndReadBack()
    {
        var entity = new EntityMetadata { LogicalName = "account" };

        Assert.Null(entity.Source);

        entity.Source = new SourceLocation("Entity.xml", 10, 5);

        Assert.NotNull(entity.Source);
        Assert.Equal("Entity.xml", entity.Source.FilePath);
        Assert.Equal(10, entity.Source.Line);
        Assert.Equal(5, entity.Source.Column);
    }

    [Fact]
    public void DefaultOwnership_IsUserOwned()
    {
        var entity = new EntityMetadata { LogicalName = "account" };
        Assert.Equal(OwnershipType.UserOwned, entity.Ownership);
    }
}
