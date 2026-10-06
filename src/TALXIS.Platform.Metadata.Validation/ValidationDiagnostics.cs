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

    /// <summary>
    /// A CMT data schema entity declares no field with updateCompare="true". An error when it has no primarynamefield either
    /// (CMT then cannot match existing records and duplicates them); a warning otherwise (CMT matches on the primary name).
    /// </summary>
    public const string CmtEntityMissingUpdateCompare = "TXM006";

    /// <summary>
    /// The CMT entityImportOrder names an entity the schema does not declare (CMT ignores it), or a declared entity is missing from it.
    /// </summary>
    public const string CmtImportOrderEntityUndeclared = "TXM007";

    /// <summary>
    /// A CMT schema entity's primaryidfield is missing, not declared as a field or not of type guid (errors), or not marked primaryKey="true" (warning).
    /// </summary>
    public const string CmtPrimaryIdFieldInvalid = "TXM008";

    /// <summary>
    /// A CMT schema entity's primarynamefield is not declared as a field.
    /// </summary>
    public const string CmtPrimaryNameFieldUndeclared = "TXM009";

    /// <summary>
    /// A CMT schema declares the same entity twice, or an entity declares the same field twice.
    /// </summary>
    public const string CmtDuplicateName = "TXM011";

    /// <summary>
    /// CMT data.xml contains an entity or field that data_schema.xml does not declare, so the import drops it.
    /// </summary>
    public const string CmtDataUndeclared = "TXM012";

    /// <summary>
    /// A CMT data.xml lookup value points to an entity the package schema does not declare; legal when the target already exists in the environment.
    /// Not reported for the system tables every export references (systemuser, team, businessunit, transactioncurrency, organization).
    /// </summary>
    public const string CmtDataLookupEntityUndeclared = "TXM013";

    /// <summary>A CMT data.xml many-to-many association uses a relationship or target entity the schema does not declare.</summary>
    public const string CmtDataManyToManyUndeclared = "TXM014";

    /// <summary>
    /// A CMT name has the wrong letter case. Errors: a schema entity or field name that is not lowercase, and a data.xml entity that matches a
    /// schema entity only when case is ignored (CMT rejects or aborts the import). Warnings: an entityImportOrder name, data.xml field, lookupentity
    /// or many-to-many name that matches only when case is ignored (CMT skips it).
    /// </summary>
    public const string CmtNameCaseMismatch = "TXM015";

    /// <summary>
    /// A CMT schema field type is not one CMT can import: not in the vocabulary, not lowercase (CMT compares case-sensitively), customer or unknown (errors).
    /// bigint (values dropped) and the TALXIS synonym "file" (rejected by Microsoft CMT) are warnings.
    /// </summary>
    public const string CmtFieldTypeNotImportable = "TXM016";

    /// <summary>
    /// A CMT data.xml record's identity is not what CMT will use: CMT creates the record under its primary-id field value, not under
    /// record@id. Errors: the field value is empty, not a GUID or differs from record@id, or record@id repeats within an entity.
    /// Warning: the record has no primary-id field (CMT generates a new id).
    /// </summary>
    public const string CmtRecordIdentityInvalid = "TXM017";

    /// <summary>A CMT dateMode value is not absolute, relative or relativeDaily; CMT cannot deserialise the schema.</summary>
    public const string CmtDateModeInvalid = "TXM018";

    /// <summary>The data.xml entities@timestamp is present but not a parseable date-time; CMT aborts the import.</summary>
    public const string CmtDataTimestampInvalid = "TXM019";

    /// <summary>A CMT schema entity filter is present but is not well-formed FetchXML with a &lt;fetch&gt; root.</summary>
    public const string CmtFilterNotFetchXml = "TXM020";

    /// <summary>
    /// A CMT data.xml lookup value (entityreference, owner) has no lookupentity, no lookupentityname, or a lookupentity
    /// outside the schema field's lookupType. CMT skips such a lookup silently and the import still succeeds.
    /// </summary>
    public const string CmtDataLookupIncomplete = "TXM021";

    /// <summary>
    /// A CMT data.xml value is not in the text form CMT reads for the field's schema type (bool other than true/false, a non-integer
    /// number, a number with a currency symbol or comma decimal, an unparseable datetime/guid/option value). CMT drops, zeroes or
    /// misreads it and the import still succeeds. Liquid templates (TALXIS renderliquid) are not checked.
    /// </summary>
    public const string CmtDataValueInvalid = "TXM022";

    /// <summary>A CMT data.xml filedata value has no payload at files/&lt;value&gt;.bin in the package folder; CMT fails that record's import.</summary>
    public const string CmtDataFilePayloadMissing = "TXM023";

    /// <summary>A CMT data.xml many-to-many association's targetentitynameidfield is not the target entity's primaryidfield.</summary>
    public const string CmtDataManyToManyTargetIdFieldInvalid = "TXM024";
}
