using System.Diagnostics;
using Microsoft.Crm.Tools.SolutionPackager;

namespace TALXIS.Platform.Metadata.Packaging;

/// <summary>
/// Wraps SolutionPackagerLib for packing and unpacking Dataverse solution ZIPs.
/// </summary>
public sealed class SolutionPackagerService : ISolutionPackagerService
{
    /// <inheritdoc />
    public void Unpack(string zipPath, string outputFolder, bool managed)
    {
        Unpack(zipPath, outputFolder, new SolutionPackagerOptions { Managed = managed });
    }

    /// <inheritdoc />
    public void Unpack(string zipPath, string outputFolder, SolutionPackagerOptions options)
    {
        ValidateUnpackArguments(zipPath, outputFolder, options);
        Directory.CreateDirectory(outputFolder);

        var arguments = new PackagerArguments
        {
            Action = CommandAction.Extract,
            PathToZipFile = zipPath,
            Folder = outputFolder,
            PackageType = options.Managed ? SolutionPackageType.Managed : SolutionPackageType.Unmanaged,
            AllowDeletes = options.AllowDeletes ? AllowDelete.Yes : AllowDelete.No,
            AllowWrites = options.AllowWrites ? AllowWrite.Yes : AllowWrite.No,
            ErrorLevel = options.ErrorLevel,
            Localize = options.Localize,
        };

        ApplyCommonOptions(arguments, options);

        var packager = new SolutionPackager(arguments);
        packager.Run();
    }

    /// <inheritdoc />
    public void Pack(string folder, string zipPath, bool managed)
    {
        Pack(folder, zipPath, new SolutionPackagerOptions { Managed = managed });
    }

    /// <inheritdoc />
    public void Pack(string folder, string zipPath, SolutionPackagerOptions options)
    {
        ValidatePackArguments(folder, zipPath, options);
        EnsureParentDirectoryExists(zipPath);

        var arguments = new PackagerArguments
        {
            Action = CommandAction.Pack,
            PathToZipFile = zipPath,
            Folder = folder,
            PackageType = options.Managed ? SolutionPackageType.Managed : SolutionPackageType.Unmanaged,
            ErrorLevel = options.ErrorLevel,
            Localize = options.Localize,
            UseUnmanagedFileForManaged = options.UseUnmanagedFileForMissingManaged,
        };

        ApplyCommonOptions(arguments, options);

        var packager = new SolutionPackager(arguments);
        packager.Run();
    }

    private static void ApplyCommonOptions(PackagerArguments arguments, SolutionPackagerOptions options)
    {
        var mappingFilePath = options.MappingFilePath;
        if (!string.IsNullOrWhiteSpace(mappingFilePath))
            arguments.MappingFile = mappingFilePath;

        var logFilePath = options.LogFilePath;
        if (!string.IsNullOrWhiteSpace(logFilePath))
        {
            EnsureParentDirectoryExists(logFilePath);
            arguments.LogFile = logFilePath;
        }

        var sourceLocale = options.SourceLocale;
        if (options.Localize && !string.IsNullOrWhiteSpace(sourceLocale))
            arguments.LocaleTemplate = sourceLocale;
    }

    private static void ValidatePackArguments(string folder, string zipPath, SolutionPackagerOptions options)
    {
        ValidateOptions(options);
        ValidatePathArgument(folder, nameof(folder), "Input folder");
        ValidatePathArgument(zipPath, nameof(zipPath), "ZIP file path");

        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException($"Input folder '{folder}' does not exist.");
    }

    private static void ValidateUnpackArguments(string zipPath, string outputFolder, SolutionPackagerOptions options)
    {
        ValidateOptions(options);
        ValidatePathArgument(zipPath, nameof(zipPath), "ZIP file path");
        ValidatePathArgument(outputFolder, nameof(outputFolder), "Output folder");

        if (!File.Exists(zipPath))
            throw new FileNotFoundException($"Solution ZIP '{zipPath}' does not exist.", zipPath);
    }

    private static void ValidateOptions(SolutionPackagerOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        var mappingFilePath = options.MappingFilePath;
        if (!string.IsNullOrWhiteSpace(mappingFilePath) && !File.Exists(mappingFilePath))
            throw new FileNotFoundException($"Mapping file '{mappingFilePath}' does not exist.", mappingFilePath);
    }

    private static void ValidatePathArgument(string value, string paramName, string displayName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{displayName} is required.", paramName);
    }

    private static void EnsureParentDirectoryExists(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
    }
}
