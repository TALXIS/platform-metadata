using System.Diagnostics;
using System.Text.Json;

namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Evaluates MSBuild properties using 'dotnet build -getProperty' (SDK 8+)
/// without running build targets.
/// </summary>
internal static class MsBuildPropertyQuery
{
    public static Dictionary<string, string> Query(string csprojPath, params string[] properties)
    {
        var arguments = $"build \"{csprojPath}\" -nologo --no-restore -nodeReuse:false"
            + string.Concat(properties.Select(p => $" -getProperty:{p}"));
        var psi = new ProcessStartInfo("dotnet", arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(csprojPath),
        };

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start 'dotnet build -getProperty' process.");

        // Drain both pipes concurrently - sequential ReadToEnd can deadlock on full buffers.
        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();

        if (!proc.WaitForExit(30_000))
        {
            try { proc.Kill(); } catch { /* best effort */ }
            throw new TimeoutException($"'dotnet build -getProperty' timed out after 30s resolving MSBuild properties for '{csprojPath}'.");
        }

        var stdout = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();

        if (proc.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"'dotnet build \"{csprojPath}\" -nologo {string.Join(" ", properties.Select(p => $"-getProperty:{p}"))}' " +
                $"failed with exit code {proc.ExitCode} while resolving MSBuild properties.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        }

        var result = new Dictionary<string, string>();
        try
        {
            using var doc = JsonDocument.Parse(stdout);
            // Multiple -getProperty switches produce {"Properties": {...}}; a single one
            // prints the bare value (handled below).
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("Properties", out var propsElement))
            {
                foreach (var property in properties)
                    result[property] = propsElement.TryGetProperty(property, out var value) ? value.GetString() ?? "" : "";
                return result;
            }
        }
        catch (JsonException)
        {
        }

        if (properties.Length == 1)
        {
            result[properties[0]] = stdout.Trim();
            return result;
        }

        throw new InvalidOperationException($"Could not parse MSBuild -getProperty output for '{csprojPath}'.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
    }

    // -getProperty path values can be project-relative and mix separators.
    public static string? ResolvePath(string? rawValue, string projectDirectory)
    {
        if (string.IsNullOrWhiteSpace(rawValue)) return null;
        var normalized = rawValue!.Replace('\\', '/');
        return Path.GetFullPath(Path.IsPathRooted(normalized) ? normalized : Path.Combine(projectDirectory, normalized));
    }
}
