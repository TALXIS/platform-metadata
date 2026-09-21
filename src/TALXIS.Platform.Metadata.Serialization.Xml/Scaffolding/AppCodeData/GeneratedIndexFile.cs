namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Inserts an export line after a marker comment and before the first empty line.
/// Rewrites the file with CRLF line endings and a trailing newline.
/// </summary>
internal static class GeneratedIndexFile
{
    public static void InsertAfterTarget(string filePath, string targetString, string settingString)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var lines = File.ReadAllLines(filePath);

        var targetIdx = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(targetString)) { targetIdx = i; break; }
        }
        if (targetIdx == -1)
            throw new InvalidOperationException($"Target '{targetString}' not found in {filePath}");

        var insertIdx = lines.Length;
        for (var i = targetIdx + 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "") { insertIdx = i; break; }
        }

        var result = new List<string>();
        for (var i = 0; i < insertIdx; i++) result.Add(lines[i]);
        result.Add(settingString);
        result.Add("");
        for (var i = insertIdx; i < lines.Length; i++)
        {
            // Skips the consumed empty line to avoid duplicating it.
            if (i == insertIdx && lines[i].Trim() == "") continue;
            result.Add(lines[i]);
        }

        WriteAllLinesCrlf(filePath, result);
    }

    // Terminates every output line with CRLF.
    internal static void WriteAllLinesCrlf(string filePath, IEnumerable<string> lines)
    {
        using var writer = new StreamWriter(filePath, append: false, new System.Text.UTF8Encoding(false)) { NewLine = "\r\n" };
        foreach (var line in lines) writer.WriteLine(line);
    }
}
