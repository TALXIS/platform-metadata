using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Merging;

namespace TALXIS.Platform.Metadata.Controls;

/// <summary>
/// Binds a PCF custom control to an existing subgrid of a form by inserting the
/// <c>controlDescriptions/controlDescription</c> node (base control block + one
/// <c>customControl</c> block per form factor), mirroring the shape produced by the
/// Dataverse form designer. Parameter values are validated against the control manifest,
/// and dataset binding values (ViewId, TargetEntityType, RelationshipName) are read from
/// the host subgrid so they never need to be supplied by hand.
/// </summary>
public static class FormControlBinding
{
    private const string SubgridClassId = "{E7A81278-8635-4D9E-8D4D-59480B391C5B}";

    public static ControlBindingResult Bind(FormMetadata form, ControlBindingRequest request)
    {
        var body = form.Body
            ?? throw new InvalidOperationException($"Form '{form.FormId}' has no body to bind a control to.");

        var host = FindHostControl(body, request.TargetControlId);
        var uniqueId = host.GetAttribute("uniqueid")
            ?? throw new InvalidOperationException($"Host control '{request.TargetControlId}' has no uniqueid attribute.");

        ValidateParameters(request.Manifest, request.Parameters);

        var parameters = BuildParameters(request, host);
        var description = new MergeableNode { Name = "controlDescription" };
        description.SetAttribute("forControl", uniqueId);

        var baseControl = new MergeableNode { Name = "customControl" };
        baseControl.SetAttribute("id", SubgridClassId);
        baseControl.AddChild(new MergeableNode { Name = "parameters" });
        description.AddChild(baseControl);

        foreach (var formFactor in new[] { "0", "1", "2" })
        {
            var factorControl = new MergeableNode { Name = "customControl" };
            factorControl.SetAttribute("formFactor", formFactor);
            factorControl.SetAttribute("name", request.ControlName);
            factorControl.AddChild(TreeMergeEngine.DeepClone(parameters));
            description.AddChild(factorControl);
        }

        var replaced = InsertDescription(body, description, uniqueId, request.Force);

        return new ControlBindingResult
        {
            ControlName = request.ControlName,
            HostControlUniqueId = uniqueId,
            ReplacedExisting = replaced,
        };
    }

    private static MergeableNode FindHostControl(MergeableNode body, string targetControlId)
    {
        var controls = body.Descendants().Where(n => n.Name == "control").ToList();
        var host = controls.FirstOrDefault(c => c.GetAttribute("id") == targetControlId)
            ?? throw new InvalidOperationException(
                $"Control '{targetControlId}' not found on the form. Available controls: " +
                string.Join(", ", controls.Select(c => c.GetAttribute("id")).Where(id => id != null)));

        if (!string.Equals(host.GetAttribute("indicationOfSubgrid"), "true", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Control '{targetControlId}' is not a subgrid. Only subgrid-bound controls are supported; " +
                "field-bound controls (e.g. VirtualDataset on a placeholder column) are not supported yet.");
        }

        return host;
    }

    private static void ValidateParameters(ControlManifestInfo manifest, IReadOnlyDictionary<string, string> values)
    {
        var byName = manifest.Properties.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();

        foreach (var pair in values)
        {
            var name = pair.Key;
            var value = pair.Value;
            if (!byName.TryGetValue(name, out var property))
            {
                errors.Add($"'{name}' is not a parameter of {manifest.QualifiedName}. Valid parameters: {string.Join(", ", manifest.Properties.Select(p => p.Name))}.");
                continue;
            }
            if (property.EnumValues.Count > 0 && !property.EnumValues.Contains(value, StringComparer.Ordinal))
                errors.Add($"'{name}': '{value}' is not an allowed value. Allowed: {string.Join(", ", property.EnumValues)}.");
            else if (property.OfType.StartsWith("Whole.", StringComparison.Ordinal) && !long.TryParse(value, out _))
                errors.Add($"'{name}': '{value}' is not a whole number ({property.OfType}).");
            else if (property.OfType == "TwoOptions" && value is not ("true" or "false"))
                errors.Add($"'{name}': '{value}' must be 'true' or 'false' (TwoOptions).");
        }

        if (errors.Count > 0)
            throw new ArgumentException("Invalid control parameters:\n  " + string.Join("\n  ", errors));
    }

    private static MergeableNode BuildParameters(ControlBindingRequest request, MergeableNode host)
    {
        var parameters = new MergeableNode { Name = "parameters" };

        // Dataset binding mirrors the host subgrid; the control's primary data-set gets the same view.
        var primaryDataSet = request.Manifest.DataSets.FirstOrDefault();
        if (primaryDataSet != null)
        {
            var hostParameters = FirstChild(host, "parameters");
            string HostValue(string name, string fallback = "") =>
                (hostParameters == null ? null : FirstChild(hostParameters, name)?.TextContent) ?? fallback;

            var viewId = HostValue("ViewId");
            var dataSet = new MergeableNode { Name = "data-set" };
            dataSet.SetAttribute("name", primaryDataSet);
            dataSet.AddChild(Leaf("ViewId", viewId));
            dataSet.AddChild(Leaf("TargetEntityType", HostValue("TargetEntityType")));
            dataSet.AddChild(Leaf("IsUserView", "false"));
            dataSet.AddChild(Leaf("EnableViewPicker", HostValue("EnableViewPicker", "false")));
            dataSet.AddChild(Leaf("RelationshipName", HostValue("RelationshipName")));
            dataSet.AddChild(Leaf("FilteredViewIds", viewId));
            parameters.AddChild(dataSet);
        }

        var byName = request.Manifest.Properties.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in request.Parameters)
        {
            var property = byName[pair.Key];
            var parameter = Leaf(property.Name, pair.Value);
            parameter.SetAttribute("type", property.OfType);
            parameter.SetAttribute("static", "true");
            parameters.AddChild(parameter);
        }

        return parameters;
    }

    /// <returns>True when an existing binding for the same host control was replaced.</returns>
    private static bool InsertDescription(MergeableNode body, MergeableNode description, string uniqueId, bool force)
    {
        var container = FirstChild(body, "controlDescriptions");
        if (container == null)
        {
            container = new MergeableNode { Name = "controlDescriptions" };
            // Match the designer's element order: controlDescriptions precedes
            // DisplayConditions / formLibraries / events.
            var anchor = FirstChild(body, "DisplayConditions") ?? FirstChild(body, "formLibraries") ?? FirstChild(body, "events");
            if (anchor != null) body.InsertChild(IndexOf(body, anchor), container);
            else body.AddChild(container);
        }

        var existing = container.Children.FirstOrDefault(d =>
            d.Name == "controlDescription" &&
            string.Equals(d.GetAttribute("forControl"), uniqueId, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            if (!force)
                throw new InvalidOperationException($"A custom control is already bound to control '{uniqueId}'.");
            var index = IndexOf(container, existing);
            container.RemoveChild(existing);
            container.InsertChild(index, description);
            return true;
        }

        container.AddChild(description);
        return false;
    }

    private static MergeableNode Leaf(string name, string value) => new() { Name = name, TextContent = value };

    private static MergeableNode? FirstChild(MergeableNode node, string name) =>
        node.Children.FirstOrDefault(c => c.Name == name);

    private static int IndexOf(MergeableNode parent, MergeableNode child)
    {
        for (var i = 0; i < parent.Children.Count; i++)
        {
            if (ReferenceEquals(parent.Children[i], child)) return i;
        }
        return -1;
    }
}
