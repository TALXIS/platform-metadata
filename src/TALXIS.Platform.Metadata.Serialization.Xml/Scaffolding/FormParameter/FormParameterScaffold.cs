namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Registers a querystringparameter on the located form's formparameters container,
/// creating the container when missing; an existing parameter with the same name
/// is left untouched.
/// </summary>
public static class FormParameterScaffold
{
    public static ScaffoldResult Apply(FormParameterScaffoldRequest request)
    {
        var formFilePath = FormXmlLocator.Locate(request.SolutionRootPath, request.EntitySchemaName, request.FormType, request.FormId);
        var formDoc = ScaffoldXmlFile.Load(formFilePath);

        var formNode = formDoc.SelectSingleNode("//form")
            ?? throw new InvalidOperationException($"Form node not found in '{formFilePath}'.");

        var parametersNode = formNode.SelectSingleNode("formparameters");
        if (parametersNode == null)
        {
            parametersNode = formDoc.CreateElement("formparameters");
            formNode.AppendChild(parametersNode);
        }

        if (parametersNode.SelectSingleNode($"querystringparameter[@name='{request.ParameterName}']") == null)
        {
            var parameter = formDoc.CreateElement("querystringparameter");
            parameter.SetAttribute("name", request.ParameterName);
            parameter.SetAttribute("type", request.ParameterType);
            parametersNode.AppendChild(parameter);
        }

        ScaffoldXmlFile.Save(formDoc, formFilePath);
        return new ScaffoldResult();
    }
}
