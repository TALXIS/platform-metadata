namespace TALXIS.Platform.Metadata.Serialization.Xml.Scaffolding;

/// <summary>
/// Picks the rendered fragment matching the located form: the dialog variant for
/// dialog forms (auto-detected from the file's folder), the regular one otherwise.
/// </summary>
internal static class FormScaffoldFragment
{
    public static string PickForForm(string formFilePath, string? requestedFormType, string regularFragmentPath, string? dialogFragmentPath)
    {
        var formType = requestedFormType ?? DetectFormType(formFilePath);
        var fragmentPath = formType == "dialog"
            ? dialogFragmentPath ?? throw new InvalidOperationException($"Dialog fragment file is required for dialog form '{formFilePath}'.")
            : regularFragmentPath;
        if (!File.Exists(fragmentPath))
            throw new FileNotFoundException($"Fragment file not found: {fragmentPath}");
        return fragmentPath;
    }

    // Derives the form type from the located file's folder name.
    private static string DetectFormType(string formFilePath)
    {
        var folderName = new FileInfo(formFilePath).Directory?.Name;
        return folderName == "Dialogs" ? "dialog" : folderName ?? "";
    }
}
