using System.Xml.Linq;

namespace TALXIS.Platform.Metadata.Serialization.Xml.ConfigurationMigration;

/// <summary>
/// The attributes the CMT model maps to properties, per element, and the reading and writing of everything else into the models'
/// <c>OtherAttributes</c>. One list per element serves both the reader and the writer, so the two cannot disagree on what is "other".
/// </summary>
internal static class CmtOtherAttributes
{
    public static readonly string[] SchemaEntity = { "name", "displayname", "etc", "primaryidfield", "primarynamefield", "disableplugins", "skipupdate", "forcecreate", "renderliquid" };

    public static readonly string[] SchemaField = { "name", "displayname", "type", "primaryKey", "updateCompare", "customfield", "lookupType", "dateMode" };

    public static readonly string[] Relationship =
    {
        "name", "manyToMany", "isreflexive", "relatedEntityName", "m2mTargetEntity", "m2mTargetEntityPrimaryKey",
        "referencingAttribute", "referencedEntity", "referencedAttribute", "referencingEntity"
    };

    public static readonly string[] DataEntity = { "name", "displayname" };

    public static readonly string[] Record = { "id", "newId" };

    public static readonly string[] DataField = { "name", "value", "filename", "lookupentity", "lookupentityname" };

    public static readonly string[] ManyToMany = { "sourceid", "targetentityname", "targetentitynameidfield", "m2mrelationshipname", "m2mrelationshipschemaname" };

    /// <summary>
    /// Copies every attribute of <paramref name="element"/> that is not in <paramref name="known"/> into <paramref name="target"/>.
    /// </summary>
    public static void Read(XElement element, IDictionary<string, string> target, string[] known)
    {
        foreach (var attribute in Others(element, known)) target[attribute.Name.ToString()] = attribute.Value;
    }

    /// <summary>
    /// Makes the attributes of <paramref name="element"/> outside <paramref name="known"/> match <paramref name="others"/>: adds and changes
    /// the ones listed, removes the ones that are not, and leaves an attribute untouched when its value is already right.
    /// </summary>
    public static void Write(XElement element, IDictionary<string, string> others, string[] known)
    {
        foreach (var stale in Others(element, known).Where(a => !others.ContainsKey(a.Name.ToString())).ToList()) stale.Remove();

        foreach (var other in others)
        {
            var name = XName.Get(other.Key);
            if (element.Attribute(name)?.Value != other.Value) element.SetAttributeValue(name, other.Value);
        }
    }

    private static IEnumerable<XAttribute> Others(XElement element, string[] known) =>
        element.Attributes().Where(a => !a.IsNamespaceDeclaration && !known.Contains(a.Name.ToString()));
}
