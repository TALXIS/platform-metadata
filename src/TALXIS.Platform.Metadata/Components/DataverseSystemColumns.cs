namespace TALXIS.Platform.Metadata.Components;

/// <summary>
/// Column names Dataverse adds to every table, shared by validation and CMT schema generation so both treat them the same way.
/// </summary>
public static class DataverseSystemColumns
{
    /// <summary>
    /// System lookups that reference platform tables (ownership, audit users, organization, currency).
    /// </summary>
    public static readonly IReadOnlyCollection<string> Referencing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "ownerid", "owningbusinessunit", "owninguser", "owningteam", "organizationid",
        "createdby", "createdonbehalfby", "modifiedby", "modifiedonbehalfby",
        "transactioncurrencyid"
    };

    /// <summary>
    /// Maintained columns the platform never accepts on create or update: the write timestamps, the row version and the owner name helpers.
    /// </summary>
    public static readonly IReadOnlyCollection<string> NotWritable = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "createdon", "modifiedon", "versionnumber", "owneridname", "owneridtype", "owneridyominame"
    };

    /// <summary>
    /// Audit, state and housekeeping columns the platform maintains itself, including <see cref="NotWritable"/>.
    /// </summary>
    public static readonly IReadOnlyCollection<string> Maintained = new HashSet<string>(
        NotWritable.Concat(new[]
        {
            "overriddencreatedon", "statecode", "statuscode", "importsequencenumber", "timezoneruleversionnumber",
            "utcconversiontimezonecode", "exchangerate"
        }),
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether <paramref name="logicalName"/> is a system column from either list.
    /// </summary>
    public static bool Contains(string logicalName)
    {
        return Referencing.Contains(logicalName) || Maintained.Contains(logicalName);
    }
}
