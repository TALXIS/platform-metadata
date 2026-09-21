using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Registers the entity data source in power.config.json and rewrites the file
/// with two-space indentation, CRLF line endings and no trailing newline.
/// </summary>
internal static class PowerConfigFile
{
    public static void AddDataSource(string filePath, string entityLogicalName)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        // Uses the second underscore-separated segment of the lowercase name plus "s";
        // a missing prefix yields just "s".
        var parts = entityLogicalName.Split('_');
        var noPrefixName = (parts.Length > 1 ? parts[1] : "") + "s";

        var json = JObject.Parse(File.ReadAllText(filePath));

        if (json["databaseReferences"] is not JObject databaseReferences)
        {
            databaseReferences = new JObject();
            json["databaseReferences"] = databaseReferences;
        }

        if (databaseReferences["default.cds"] is not JObject defaultCds)
        {
            defaultCds = new JObject
            {
                ["dataSources"] = new JObject(),
                ["environmentVariableName"] = "",
            };
            databaseReferences["default.cds"] = defaultCds;
        }

        if (defaultCds["dataSources"] is not JObject dataSources)
        {
            dataSources = new JObject();
            defaultCds["dataSources"] = dataSources;
        }

        if (dataSources[noPrefixName] == null)
        {
            dataSources[noPrefixName] = new JObject
            {
                ["entitySetName"] = entityLogicalName + "s",
                ["logicalName"] = entityLogicalName,
            };
        }

        using var stringWriter = new StringWriter { NewLine = "\r\n" };
        using var jsonWriter = new JsonTextWriter(stringWriter)
        {
            Formatting = Formatting.Indented,
            Indentation = 2,
            IndentChar = ' ',
        };
        json.WriteTo(jsonWriter);
        jsonWriter.Flush();

        File.WriteAllText(filePath, stringWriter.ToString(), new System.Text.UTF8Encoding(false));
    }
}
