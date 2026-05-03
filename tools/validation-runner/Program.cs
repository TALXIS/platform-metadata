using TALXIS.Platform.Metadata.Validation;
var path = args[0];
var solutionFiles = Directory.GetFiles(path, "Solution.xml", SearchOption.AllDirectories)
    .Where(f => f.Contains(Path.Combine("Other", "Solution.xml"))).OrderBy(f => f).ToList();
Console.WriteLine($"Found {solutionFiles.Count} solutions in {Path.GetFileName(path)}");
var validator = new WorkspaceValidator();
int totalErrors = 0, totalWarnings = 0, totalLoaded = 0;
foreach (var sf in solutionFiles)
{
    var dir = Path.GetDirectoryName(Path.GetDirectoryName(sf))!;
    var rel = Path.GetRelativePath(path, dir);
    var report = validator.ValidateDirectory(dir);
    var errors = report.Results.Count(r => r.Severity == ValidationSeverity.Error);
    var warns = report.Results.Count(r => r.Severity == ValidationSeverity.Warning && !r.Message.StartsWith("Could not find schema"));
    totalErrors += errors; totalWarnings += warns;
    var model = report.LoadedComponents != null ? report.LoadedComponents.ToString() : "FAILED";
    if (report.LoadedComponents != null) totalLoaded++;
    var status = errors > 0 ? "FAIL" : "OK";
    Console.WriteLine($"  [{status}] {rel}: {errors} errors, {warns} significant warnings | {model}");
    foreach (var e in report.Results.Where(r => r.Severity == ValidationSeverity.Error).Take(3))
        Console.WriteLine($"    ERROR: {e.Message}");
}
Console.WriteLine($"\nTotal: {solutionFiles.Count} solutions, {totalLoaded} loaded, {totalErrors} errors, {totalWarnings} significant warnings");
