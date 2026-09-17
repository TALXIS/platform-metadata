using System.Text;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Writes the environmentvariablevalues.json payload (id, iscustomizable and
/// the verbatim value) over the rendered stub using UTF-8 without a BOM
/// and with a trailing newline.
/// </summary>
public static class EnvironmentVariableValueScaffold
{
    public static ScaffoldResult Apply(EnvironmentVariableValueScaffoldRequest request)
    {
        if (!File.Exists(request.ValuesFilePath))
            throw new FileNotFoundException($"environmentvariablevalues.json stub not found: {request.ValuesFilePath}");

        var json =
            "{\n" +
            "  \"environmentvariablevalues\": {\n" +
            "    \"environmentvariablevalue\": {\n" +
            $"      \"@environmentvariablevalueid\": {Quote(request.ValueId)},\n" +
            "      \"iscustomizable\": \"1\",\n" +
            $"      \"value\": {Quote(request.Value)}\n" +
            "    }\n" +
            "  }\n" +
            "}\n";

        File.WriteAllText(request.ValuesFilePath, json, new UTF8Encoding(false));
        return new ScaffoldResult();
    }

    private static string Quote(string value)
    {
        var sb = new StringBuilder("\"");
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        return sb.Append('"').ToString();
    }
}
