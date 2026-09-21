using System.Xml.Linq;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Creates or extends .power/schemas/appschemas/dataSourcesInfo.ts with the
/// entity's entry. Uses LF line endings on creation and CRLF when appending
/// through WriteAllLines.
/// </summary>
internal static class DataSourcesInfoFile
{
    public static void AddEntry(string filePath, string modelSolutionRootPath, string entityLogicalName)
    {
        var entityXmlPath = Path.Combine(modelSolutionRootPath, "Entities", entityLogicalName, "Entity.xml");
        if (!File.Exists(entityXmlPath))
            throw new FileNotFoundException($"Entity.xml not found: {entityXmlPath}");

        var doc = XDocument.Load(entityXmlPath);
        var entityNode = doc.Root!.Element("EntityInfo")!.Element("entity")!;
        var entitySetName = entityNode.Element("EntitySetName")!.Value;

        var primaryKey = entityNode.Element("attributes")!.Elements("attribute")
            .FirstOrDefault(a => a.Element("Type")?.Value == "primarykey")
            ?.Element("LogicalName")?.Value
            ?? throw new InvalidOperationException($"Primary key not found in {entityXmlPath}");

        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var entryLines = new[]
        {
            $"  \"{entitySetName}\": {{",
            "    \"tableId\": \"\",",
            "    \"version\": \"\",",
            $"    \"primaryKey\": \"{primaryKey}\",",
            "    \"dataSourceType\": \"Dataverse\",",
            "    \"apis\": {}",
            "  }",
        };

        if (!File.Exists(filePath))
        {
            var content = string.Join("\n", new[]
            {
                "/*!",
                " * Copyright (C) Microsoft Corporation. All rights reserved.",
                " * This file is auto-generated. Do not modify it manually.",
                " * Changes to this file may be overwritten.",
                " */",
                "",
                "export const dataSourcesInfo = {",
            }.Concat(entryLines).Concat(new[] { "};" }));
            File.WriteAllText(filePath, content + "\n", new System.Text.UTF8Encoding(false));
            return;
        }

        var text = File.ReadAllText(filePath);
        if (text.Contains($"\"{entitySetName}\"")) return;

        var lines = File.ReadAllLines(filePath);

        var closingIdx = -1;
        for (var i = lines.Length - 1; i >= 0; i--)
        {
            if (lines[i].TrimStart() == "};") { closingIdx = i; break; }
        }
        if (closingIdx == -1)
            throw new InvalidOperationException($"Could not find closing '}};' in {filePath}");

        var lastEntryClose = -1;
        for (var i = closingIdx - 1; i >= 0; i--)
        {
            if (lines[i].TrimStart() == "}") { lastEntryClose = i; break; }
        }

        var result = new List<string>();
        for (var i = 0; i < lines.Length; i++)
        {
            if (i == lastEntryClose && lastEntryClose != -1)
            {
                result.Add(lines[i] + ",");
                result.AddRange(entryLines);
            }
            else if (i == closingIdx && lastEntryClose == -1)
            {
                result.AddRange(entryLines);
                result.Add(lines[i]);
            }
            else
            {
                result.Add(lines[i]);
            }
        }

        GeneratedIndexFile.WriteAllLinesCrlf(filePath, result);
    }
}
