using System.Xml;
using System.Xml.Schema;
using TALXIS.Platform.Metadata.Schema;

namespace TALXIS.Platform.Metadata.Validation;

/// <summary>
/// Walks a compiled <see cref="XmlSchemaSet"/> to extract a <see cref="ComponentSchema"/>
/// describing the expected XML structure for a given root element.
/// </summary>
public sealed class XsdSchemaIntrospector : ISchemaIntrospector
{
    private const int MaxDepth = 10;

    private readonly XmlSchemaSet _schemaSet;

    public XsdSchemaIntrospector()
    {
        _schemaSet = new XmlSchemaSet();
        foreach (var schemaFile in SchemaResourceLoader.GetAvailableSchemas()
                     .Where(f => f.EndsWith(".xsd", StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = SchemaResourceLoader.OpenSchema(schemaFile);
            _schemaSet.Add(null, XmlReader.Create(stream));
        }
        _schemaSet.Compile();
    }

    /// <summary>
    /// Returns the schema structure for the given root element name, or null if not found.
    /// </summary>
    public ComponentSchema? GetSchema(string rootElementName)
    {
        var qname = new XmlQualifiedName(rootElementName);
        var element = _schemaSet.GlobalElements[qname] as XmlSchemaElement;
        if (element == null)
            return null;

        var (children, attributes) = WalkType(element.ElementSchemaType, 0);
        return new ComponentSchema(
            rootElementName,
            children ?? Array.Empty<SchemaElement>(),
            attributes ?? Array.Empty<SchemaAttribute>());
    }

    /// <summary>
    /// Returns the schema for a registered <see cref="ComponentType"/>, mapping through the
    /// <see cref="ComponentDefinitionRegistry"/> to find the serialized root element name.
    /// </summary>
    public ComponentSchema? GetSchemaForComponentType(ComponentType type)
    {
        var def = ComponentDefinitionRegistry.GetByType(type);
        if (def == null) return null;
        // Try SerializedName first (plural container roots like ConnectionRoles,
        // FieldSecurityProfiles), then fall back to Name (Entity, Role).
        return GetSchema(def.SerializedName) ?? GetSchema(def.Name);
    }

    private (IReadOnlyList<SchemaElement>? Elements, IReadOnlyList<SchemaAttribute>? Attributes) WalkType(
        XmlSchemaType? schemaType, int depth, bool parentOptional = false)
    {
        if (schemaType is XmlSchemaComplexType complexType && depth < MaxDepth)
        {
            var elements = WalkParticle(complexType.Particle, depth, parentOptional);
            var attributes = WalkAttributes(complexType.Attributes);

            // Also check ContentTypeParticle for types using simpleContent/complexContent extensions
            if (elements.Count == 0 && complexType.ContentTypeParticle is XmlSchemaGroupBase groupBase)
            {
                elements = WalkGroupBase(groupBase, depth, parentOptional);
            }

            return (
                elements.Count > 0 ? elements : null,
                attributes.Count > 0 ? attributes : null);
        }

        return (null, null);
    }

    private List<SchemaElement> WalkParticle(XmlSchemaParticle? particle, int depth, bool parentOptional = false)
    {
        if (particle is XmlSchemaGroupBase groupBase)
            return WalkGroupBase(groupBase, depth, parentOptional);

        if (particle is XmlSchemaGroupRef groupRef)
        {
            var resolvedParticle = groupRef.Particle;
            if (resolvedParticle != null)
            {
                bool groupOptional = parentOptional || groupRef.MinOccurs == 0;
                return WalkGroupBase(resolvedParticle, depth, groupOptional);
            }
        }

        if (particle is XmlSchemaAny)
        {
            return new List<SchemaElement>
            {
                new SchemaElement("*", required: false, maxOccurs: null, typeName: "any",
                    allowedValues: null, children: null, attributes: null)
            };
        }

        return new List<SchemaElement>();
    }

    private List<SchemaElement> WalkGroupBase(XmlSchemaGroupBase groupBase, int depth, bool parentOptional = false)
    {
        bool isOptionalContext = parentOptional
            || groupBase.MinOccurs == 0
            || groupBase is XmlSchemaChoice;

        var result = new List<SchemaElement>();
        foreach (var item in groupBase.Items)
        {
            if (item is XmlSchemaElement childElement)
            {
                result.Add(BuildElement(childElement, depth + 1, isOptionalContext));
            }
            else if (item is XmlSchemaGroupBase nestedGroup)
            {
                result.AddRange(WalkGroupBase(nestedGroup, depth, isOptionalContext));
            }
            else if (item is XmlSchemaGroupRef nestedRef)
            {
                var resolvedParticle = nestedRef.Particle;
                if (resolvedParticle != null)
                {
                    bool refOptional = isOptionalContext || nestedRef.MinOccurs == 0;
                    result.AddRange(WalkGroupBase(resolvedParticle, depth, refOptional));
                }
            }
            else if (item is XmlSchemaAny)
            {
                result.Add(new SchemaElement("*", required: false, maxOccurs: null, typeName: "any",
                    allowedValues: null, children: null, attributes: null));
            }
        }
        return result;
    }

    private SchemaElement BuildElement(XmlSchemaElement element, int depth, bool parentOptional = false)
    {
        bool required = element.MinOccurs > 0 && !parentOptional;
        int? maxOccurs = element.MaxOccursString == "unbounded" ? null : (int)element.MaxOccurs;

        var (typeName, allowedValues) = ResolveTypeName(element.ElementSchemaType);
        var (children, attributes) = WalkType(element.ElementSchemaType, depth, parentOptional);

        return new SchemaElement(
            element.Name ?? element.QualifiedName.Name,
            required,
            maxOccurs,
            typeName,
            allowedValues,
            children,
            attributes);
    }

    private static (string? TypeName, IReadOnlyList<string>? AllowedValues) ResolveTypeName(XmlSchemaType? type)
    {
        if (type == null)
            return (null, null);

        // Simple type with enumeration restrictions
        if (type is XmlSchemaSimpleType simpleType)
        {
            var enumValues = ExtractEnumValues(simpleType);
            if (enumValues != null)
                return ("enum", enumValues);

            return (SimplifyTypeName(simpleType.QualifiedName), null);
        }

        // Complex type with simple content (extension of a simple type)
        if (type is XmlSchemaComplexType complexType && complexType.ContentModel is XmlSchemaSimpleContent)
        {
            var baseType = complexType.BaseXmlSchemaType;
            if (baseType is XmlSchemaSimpleType baseSimple)
            {
                var enumValues = ExtractEnumValues(baseSimple);
                if (enumValues != null)
                    return ("enum", enumValues);
                return (SimplifyTypeName(baseSimple.QualifiedName), null);
            }
            return (SimplifyTypeName(type.QualifiedName), null);
        }

        if (type is XmlSchemaComplexType)
            return ("complex", null);

        return (SimplifyTypeName(type.QualifiedName), null);
    }

    private static IReadOnlyList<string>? ExtractEnumValues(XmlSchemaSimpleType simpleType)
    {
        if (simpleType.Content is XmlSchemaSimpleTypeRestriction restriction)
        {
            var values = new List<string>();
            foreach (var facet in restriction.Facets)
            {
                if (facet is XmlSchemaEnumerationFacet enumFacet)
                    values.Add(enumFacet.Value);
            }
            if (values.Count > 0)
                return values;
        }
        return null;
    }

    private static string? SimplifyTypeName(XmlQualifiedName qname)
    {
        if (qname.IsEmpty)
            return null;

        if (qname.Namespace == "http://www.w3.org/2001/XMLSchema")
        {
            return qname.Name switch
            {
                "string" => "string",
                "boolean" => "boolean",
                "int" or "integer" or "nonNegativeInteger" or "positiveInteger"
                    or "negativeInteger" or "nonPositiveInteger"
                    or "short" or "long" or "unsignedInt" or "unsignedShort" or "unsignedLong"
                    or "byte" or "unsignedByte" => "integer",
                "decimal" or "float" or "double" => "decimal",
                "date" or "dateTime" or "time" => "datetime",
                "anyURI" => "uri",
                _ => qname.Name,
            };
        }

        return qname.Name;
    }

    private List<SchemaAttribute> WalkAttributes(XmlSchemaObjectCollection attributes)
    {
        var result = new List<SchemaAttribute>();
        foreach (var item in attributes)
        {
            if (item is XmlSchemaAttribute attr)
            {
                var (typeName, allowedValues) = ResolveAttributeType(attr);
                result.Add(new SchemaAttribute(
                    attr.Name ?? attr.QualifiedName.Name,
                    attr.Use == XmlSchemaUse.Required,
                    typeName,
                    allowedValues));
            }
            else if (item is XmlSchemaAttributeGroupRef groupRef)
            {
                var resolved = _schemaSet.AttributeGroups[groupRef.RefName] as XmlSchemaAttributeGroup;
                if (resolved != null)
                {
                    result.AddRange(WalkAttributes(resolved.Attributes));
                }
            }
            else if (item is XmlSchemaAnyAttribute)
            {
                result.Add(new SchemaAttribute("*", required: false, typeName: "any", allowedValues: null));
            }
        }
        return result;
    }

    private static (string? TypeName, IReadOnlyList<string>? AllowedValues) ResolveAttributeType(XmlSchemaAttribute attr)
    {
        if (attr.AttributeSchemaType is XmlSchemaSimpleType simpleType)
        {
            var enumValues = ExtractEnumValues(simpleType);
            if (enumValues != null)
                return ("enum", enumValues);

            return (SimplifyTypeName(simpleType.QualifiedName), null);
        }

        return (SimplifyTypeName(attr.SchemaTypeName), null);
    }
}
