namespace TALXIS.Platform.Metadata.DataMigration;

/// <summary>
/// When two copies of a data.xml field count as the same, shared by package merging and the repeated-record check (TXM017) so both
/// compare copies by the same rules.
/// </summary>
internal static class CmtDataComparison
{
    /// <summary>
    /// Whether the two fields carry everything the importer reads alike: the value, the lookup target and its fallback name, the file
    /// name, attributes the model does not know, and for a party list (whose value is empty) the attendee records, in order.
    /// </summary>
    public static bool SameContent(CmtDataField first, CmtDataField later)
    {
        if (first.Value != later.Value || first.LookupEntity != later.LookupEntity || first.LookupEntityName != later.LookupEntityName) return false;
        if (first.FileName != later.FileName || !SameOtherAttributes(first.OtherAttributes, later.OtherAttributes)) return false;

        return SameAttendees(first.ActivityPointerRecords, later.ActivityPointerRecords);
    }

    private static bool SameOtherAttributes(IDictionary<string, string> first, IDictionary<string, string> later)
    {
        return first.Count == later.Count && first.All(other => later.TryGetValue(other.Key, out var value) && value == other.Value);
    }

    private static bool SameAttendees(IList<CmtDataRecord> first, IList<CmtDataRecord> later)
    {
        if (first.Count != later.Count) return false;

        for (var i = 0; i < first.Count; i++)
        {
            if (first[i].Id != later[i].Id || first[i].Fields.Count != later[i].Fields.Count) return false;

            for (var j = 0; j < first[i].Fields.Count; j++)
            {
                var (attendee, laterAttendee) = (first[i].Fields[j], later[i].Fields[j]);
                if (attendee.Name != laterAttendee.Name || !SameContent(attendee, laterAttendee)) return false;
            }
        }

        return true;
    }
}
