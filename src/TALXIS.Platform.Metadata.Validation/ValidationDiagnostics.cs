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

    /// <summary>A CMT data schema entity declares no field with updateCompare="true", so configuration imports cannot match existing records.</summary>
    public const string CmtEntityMissingUpdateCompare = "TXM006";

    /// <summary>
    /// The CMT entityImportOrder names an entity the schema does not declare, or a declared entity is missing from it.
    /// </summary>
    public const string CmtImportOrderEntityUndeclared = "TXM007";

    /// <summary>
    /// A CMT schema entity's primaryidfield is missing, not declared as a field, not marked primaryKey="true" or not of type guid.
    /// </summary>
    public const string CmtPrimaryIdFieldInvalid = "TXM008";

    /// <summary>
    /// A CMT schema entity's primarynamefield is not declared as a field.
    /// </summary>
    public const string CmtPrimaryNameFieldUndeclared = "TXM009";

    /// <summary>
    /// A CMT schema lookup field (entityreference, customer, owner) does not say which entity it points to.
    /// </summary>
    public const string CmtLookupTypeMissing = "TXM010";

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
    /// </summary>
    public const string CmtDataLookupEntityUndeclared = "TXM013";

    /// <summary>
    /// A CMT data.xml many-to-many association uses a relationship or target entity the schema does not declare.
    /// </summary>
    public const string CmtDataManyToManyUndeclared = "TXM014";
}
