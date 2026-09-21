using System.Xml;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Locates the command definition and its JavaScriptFunction in the entity ribbon
/// and appends the rendered parameters. Failed lookups list the available candidates;
/// an empty payload produces a warning and leaves the ribbon unchanged.
/// </summary>
public static class RibbonCommandParameterScaffold
{
    public static ScaffoldResult Apply(RibbonCommandParameterScaffoldRequest request)
    {
        var result = new ScaffoldResult();
        var ribbonDoc = new XmlDocument();
        ribbonDoc.Load(request.RibbonDiffFilePath);

        var command = ribbonDoc.SelectSingleNode($"//CommandDefinition[@Id='{request.CommandDefinitionId}']");
        if (command == null)
        {
            var available = string.Join(", ", ribbonDoc.SelectNodes("//CommandDefinition")!
                .Cast<XmlElement>().Select(c => c.GetAttribute("Id")));
            throw new InvalidOperationException(
                $"CommandDefinition '{request.CommandDefinitionId}' not found in RibbonDiff.xml. Available: {available}");
        }

        var function = command.SelectSingleNode($".//JavaScriptFunction[@FunctionName='{request.FunctionName}']");
        if (function == null)
        {
            var available = string.Join(", ", command.SelectNodes(".//JavaScriptFunction")!
                .Cast<XmlElement>().Select(f => f.GetAttribute("FunctionName")));
            throw new InvalidOperationException(
                $"JavaScriptFunction '{request.FunctionName}' not found in CommandDefinition '{request.CommandDefinitionId}'. Available: {available}");
        }

        var parametersDoc = new XmlDocument();
        parametersDoc.Load(request.ParametersFilePath);
        var parameters = parametersDoc.SelectNodes("//Parameters/*")!;
        if (parameters.Count == 0)
        {
            result.AddWarning("No parameters found in parameter.xml");
            return result;
        }

        foreach (XmlNode parameter in parameters)
        {
            function.AppendChild(ribbonDoc.ImportNode(parameter, true));
        }

        ribbonDoc.Save(request.RibbonDiffFilePath);
        return result;
    }
}
