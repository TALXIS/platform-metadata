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
        var arguments = new PackagerArguments
        {
            Action = CommandAction.Extract,
            PathToZipFile = zipPath,
            Folder = outputFolder,
            PackageType = managed ? SolutionPackageType.Managed : SolutionPackageType.Unmanaged,
            AllowDeletes = AllowDelete.Yes,
            AllowWrites = AllowWrite.Yes,
            ErrorLevel = TraceLevel.Info,
        };

        var packager = new SolutionPackager(arguments);
        packager.Run();
    }

    /// <inheritdoc />
    public void Pack(string folder, string zipPath, bool managed)
    {
        var arguments = new PackagerArguments
        {
            Action = CommandAction.Pack,
            PathToZipFile = zipPath,
            Folder = folder,
            PackageType = managed ? SolutionPackageType.Managed : SolutionPackageType.Unmanaged,
            ErrorLevel = TraceLevel.Info,
        };

        var packager = new SolutionPackager(arguments);
        packager.Run();
    }
}
