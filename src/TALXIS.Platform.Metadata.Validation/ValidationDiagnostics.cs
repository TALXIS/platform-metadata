namespace TALXIS.Platform.Metadata.Validation;

/// <summary>
/// Stable diagnostic codes for validation rules. Consumers use these to track or suppress an
/// individual rule instead of a whole validation stage, so a code is never reused or renumbered
/// once published.
/// </summary>
public static class ValidationDiagnostics
{
    /// <summary>Finding produced by a rule that has not been assigned a code yet.</summary>
    public const string Unclassified = "TXM000";

    /// <summary>A root component declared in Solution.xml has no matching component file in the solution.</summary>
    public const string RootComponentFileAbsent = "TXM001";

    /// <summary>A component exists in the solution source but is not declared as a root component.</summary>
    public const string ComponentNotDeclaredAsRootComponent = "TXM002";

    /// <summary>An SDK message processing step references a plugin assembly that is not part of this source — it may legitimately ship from another solution.</summary>
    public const string StepAssemblyNotInSolution = "TXM003";

    /// <summary>A solution manifest declares more than one root component of the same type with the same identity (schema name or id).</summary>
    public const string DuplicateRootComponent = "TXM004";

    /// <summary>The directory passed to solution validation has no Other/Solution.xml manifest.</summary>
    public const string SolutionManifestFileAbsent = "TXM005";

    /// <summary>A CMT data schema entity declares no field with updateCompare="true", so CMT matches existing records on the primary name or not at all.</summary>
    public const string CmtEntityMissingUpdateCompare = "TXM006";

    /// <summary>The CMT entityImportOrder names an entity the schema does not declare, or a declared entity is missing from it.</summary>
    public const string CmtImportOrderEntityUndeclared = "TXM007";

    /// <summary>A CMT schema entity's primaryidfield is missing, undeclared, not of type guid or not marked primaryKey.</summary>
    public const string CmtPrimaryIdFieldInvalid = "TXM008";

    /// <summary>A CMT schema entity's primarynamefield is not declared as a field.</summary>
    public const string CmtPrimaryNameFieldUndeclared = "TXM009";

    // TXM010 (lookupType missing) was withdrawn before release and is not reused.

    /// <summary>A CMT schema declares the same entity twice, or an entity declares the same field twice.</summary>
    public const string CmtDuplicateName = "TXM011";

    /// <summary>CMT data.xml contains an entity or field that data_schema.xml does not declare.</summary>
    public const string CmtDataUndeclared = "TXM012";

    /// <summary>A CMT data.xml lookup points to an entity the package does not declare, other than the system tables every export references.</summary>
    public const string CmtDataLookupEntityUndeclared = "TXM013";

    /// <summary>A CMT data.xml many-to-many association uses a relationship or target entity the schema does not declare.</summary>
    public const string CmtDataManyToManyUndeclared = "TXM014";

    /// <summary>A CMT name is not lowercase or matches its declaration only when letter case is ignored.</summary>
    public const string CmtNameCaseMismatch = "TXM015";

    /// <summary>A CMT schema field type is missing or is not one CMT can import.</summary>
    public const string CmtFieldTypeNotImportable = "TXM016";

    /// <summary>A CMT data.xml record's primary-id field is absent, empty, not a GUID or differs from record@id, or record@id repeats.</summary>
    public const string CmtRecordIdentityInvalid = "TXM017";

    /// <summary>A CMT dateMode value is not absolute, relative or relativeDaily.</summary>
    public const string CmtDateModeInvalid = "TXM018";

    /// <summary>The CMT data.xml timestamp is present but not a parseable date-time.</summary>
    public const string CmtDataTimestampInvalid = "TXM019";

    /// <summary>A CMT schema entity filter is not well-formed FetchXML with a &lt;fetch&gt; root.</summary>
    public const string CmtFilterNotFetchXml = "TXM020";

    /// <summary>A CMT data.xml lookup value has no lookupentity or lookupentityname, or a lookupentity outside the field's lookupType.</summary>
    public const string CmtDataLookupIncomplete = "TXM021";

    /// <summary>A CMT data.xml value is not in the text form CMT reads for the field's schema type.</summary>
    public const string CmtDataValueInvalid = "TXM022";

    /// <summary>A CMT data.xml filedata value has no payload at files/&lt;value&gt;.bin in the package folder.</summary>
    public const string CmtDataFilePayloadMissing = "TXM023";

    /// <summary>A CMT data.xml many-to-many association's targetentitynameidfield is not the target entity's primaryidfield.</summary>
    public const string CmtDataManyToManyTargetIdFieldInvalid = "TXM024";

    /// <summary>Two CMT data.xml records of one entity share the values CMT matches existing records on.</summary>
    public const string CmtDataDuplicateMatchKey = "TXM025";

    /// <summary>
    /// The CMT entityImportOrder imports an entity before an entity it looks up. CMT fills those lookups in its second pass, so this only
    /// flags an order that disagrees with the lookups, usually on purpose.
    /// </summary>
    public const string CmtImportOrderChildBeforeParent = "TXM026";

    /// <summary>A form control's datafieldname is not a column of the form's entity.</summary>
    public const string FormControlColumnUnknown = "TXM027";

    /// <summary>
    /// A column is placed more than once in a form's body (header and footer excluded). Dataverse allows this, so it is a warning
    /// that the extra placement is probably an accidental copy.
    /// </summary>
    public const string FormColumnRepeated = "TXM028";

    /// <summary>Two controls in one form share the same id.</summary>
    public const string FormControlIdDuplicate = "TXM029";

    /// <summary>A subgrid's TargetEntityType names an entity that does not exist; reported only when it misspells a workspace entity.</summary>
    public const string SubgridTargetEntityUnknown = "TXM030";

    /// <summary>
    /// A subgrid's RelationshipName is not a one-to-many from the form's entity to the subgrid's target (or a many-to-many between
    /// them), or is missing from a workspace that fully defines both entities.
    /// </summary>
    public const string SubgridRelationshipInvalid = "TXM031";

    /// <summary>A subgrid's ViewId is a saved query of another entity, or is missing although the workspace fully defines the target entity.</summary>
    public const string SubgridViewInvalid = "TXM032";

    /// <summary>A view's fetchxml &lt;attribute&gt;, &lt;order&gt; or &lt;condition&gt; names a column its entity does not have.</summary>
    public const string ViewFetchColumnUnknown = "TXM033";

    /// <summary>A view's layoutxml &lt;cell&gt; names a column of the view's entity, or of a link-entity alias, that does not exist.</summary>
    public const string ViewLayoutColumnUnknown = "TXM034";

    /// <summary>A site map SubArea's Entity or Url etn= names an entity that does not exist; reported only when it misspells a workspace entity.</summary>
    public const string SiteMapEntityUnknown = "TXM035";

    /// <summary>A site map SubArea's Url viewid= is not a saved query of the etn= entity.</summary>
    public const string SiteMapViewInvalid = "TXM036";

    /// <summary>An app module component (entity, form, view or site map) is not in the workspace.</summary>
    public const string AppModuleComponentMissing = "TXM037";
}
