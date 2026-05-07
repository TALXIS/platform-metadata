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
        if (!string.IsNullOrEmpty(options.MappingFilePath))
            arguments.MappingFile = options.MappingFilePath;

        if (!string.IsNullOrEmpty(options.LogFilePath))
            arguments.LogFile = options.LogFilePath;

        if (!string.IsNullOrEmpty(options.SourceLocale))
            arguments.LocaleTemplate = options.SourceLocale;
    }
}
