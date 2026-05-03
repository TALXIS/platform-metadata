using TALXIS.Platform.Metadata.Validation;
var path = args[0];
var validator = new WorkspaceValidator();
var report = validator.ValidateDirectory(path);
var errors = report.Results.Where(r => r.Severity == ValidationSeverity.Error).ToList();
var warns = report.Results.Where(r => r.Severity == ValidationSeverity.Warning && !r.Message.StartsWith("Could not find schema")).ToList();
Console.WriteLine($"Errors: {errors.Count}, Warnings: {warns.Count}");
if (report.LoadedComponents != null) Console.WriteLine($"Model: {report.LoadedComponents}");
foreach (var e in errors)
{
    var file = e.FilePath != null ? Path.GetRelativePath(path, e.FilePath) : "?";
    Console.WriteLine($"  ERROR: [{file}:{e.Line}] {e.Message}");
}
foreach (var w in warns.Take(5))
{
    var file = w.FilePath != null ? Path.GetRelativePath(path, w.FilePath) : "?";
    Console.WriteLine($"  WARN: [{file}] {w.Message}");
}
