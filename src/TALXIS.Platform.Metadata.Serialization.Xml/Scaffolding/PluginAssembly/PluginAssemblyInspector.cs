using System.Reflection;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Metadata-only inspection of the built plugin assembly: public key token and the
/// public IPlugin implementations, without loading the dll for execution.
/// </summary>
internal static class PluginAssemblyInspector
{
    public static (string PublicKeyToken, List<string> PluginClassNames) Inspect(string dllPath, string sdkPath)
    {
        var resolverPaths = new List<string>();
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
        if (runtimeDir != null) resolverPaths.AddRange(Directory.GetFiles(runtimeDir, "*.dll"));
        resolverPaths.AddRange(Directory.GetFiles(Path.GetDirectoryName(dllPath)!, "*.dll"));
        resolverPaths.Add(sdkPath);

        using var context = new MetadataLoadContext(new PathAssemblyResolver(resolverPaths));
        var pluginAssembly = context.LoadFromAssemblyPath(dllPath);

        var token = pluginAssembly.GetName().GetPublicKeyToken();
        if (token == null || token.Length == 0) throw new InvalidOperationException("Build not signed");
        var publicKeyToken = BitConverter.ToString(token).Replace("-", "").ToLower();

        var classList = pluginAssembly.GetTypes()
            .Where(t => t.IsClass && t.IsPublic &&
                t.GetInterfaces().Any(i => i.FullName == "Microsoft.Xrm.Sdk.IPlugin"))
            .Select(t => t.FullName!)
            .ToList();

        if (!classList.Any()) throw new InvalidOperationException("Plugins not found");

        return (publicKeyToken, classList);
    }
}
