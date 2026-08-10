namespace TALXIS.Platform.Metadata.Packaging;

/// <summary>
/// Packs and unpacks Dataverse solution ZIPs using SolutionPackager.
/// No Dataverse connection required — operates on local files only.
/// </summary>
public interface ISolutionPackagerService
{
    /// <summary>
    /// Packs a folder structure into a solution ZIP file.
    /// </summary>
    SolutionPackagerResult Pack(string folder, string zipPath, bool managed);

    /// <summary>
    /// Packs a folder structure into a solution ZIP file with additional options.
    /// </summary>
    SolutionPackagerResult Pack(string folder, string zipPath, SolutionPackagerOptions options);

    /// <summary>
    /// Unpacks a solution ZIP file into a folder structure.
    /// </summary>
    SolutionPackagerResult Unpack(string zipPath, string outputFolder, bool managed);

    /// <summary>
    /// Unpacks a solution ZIP file into a folder structure with additional options.
    /// </summary>
    SolutionPackagerResult Unpack(string zipPath, string outputFolder, SolutionPackagerOptions options);
}
