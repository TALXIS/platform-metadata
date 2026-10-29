using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Constants and the LF-only, BOM-less writer shared by the logic flow codegen files.
/// </summary>
internal static class LogicFlowCodegen
{
    public const string LogicFlowsApiId = "/providers/Microsoft.PowerApps/apis/shared_logicflows";
    public const string ApiVersion = "2015-02-01-preview";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static void WriteText(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content.Replace("\r\n", "\n"), new UTF8Encoding(false));
    }
}
