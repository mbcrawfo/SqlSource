using System;
using System.IO;
using System.Linq;
using SqlSource.Diagnostics;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Projects;

/// <summary>
/// Finds the unit of a run from the path on the command line, the way <c>dotnet build</c> finds what to build.
/// </summary>
internal static class RunUnitFinder
{
    /// <summary>
    /// The unit, or null after reporting why there is none.
    /// </summary>
    /// <param name="argument">The path as given, or null when none was.</param>
    /// <param name="workingDirectory">The full path a relative path is resolved against.</param>
    /// <param name="reporter">Where the error goes.</param>
    public static RunUnit? Find(string? argument, string workingDirectory, Reporter reporter)
    {
        // Path.GetFullPath throws for an empty path, which is what an unset variable of a shell gives.
        if (argument is { Length: 0 })
        {
            reporter.Report(ToolDiagnostic.Create(ToolDiagnostics.NotARunUnit, argument));
            return null;
        }

        var path = Path.TrimEndingDirectorySeparator(
            argument is null ? workingDirectory : Path.GetFullPath(argument, workingDirectory)
        );

        if (Directory.Exists(path))
        {
            return FindIn(path, reporter);
        }

        if (File.Exists(path) && KindOf(path) is { } kind)
        {
            return new RunUnit(kind, path);
        }

        reporter.Report(ToolDiagnostic.Create(ToolDiagnostics.NotARunUnit, path));
        return null;
    }

    private static RunUnit? FindIn(string directory, Reporter reporter)
    {
        // Only files: a folder named App.csproj is not a project.
        var units = Directory
            .EnumerateFiles(directory)
            .Select(static file => (File: file, Kind: KindOf(file)))
            .Where(static unit => unit.Kind is not null)
            .Take(2)
            .ToArray();

        switch (units)
        {
            case [{ Kind: { } kind } unit]:
                return new RunUnit(kind, unit.File);
            case []:
                reporter.Report(ToolDiagnostic.Create(ToolDiagnostics.NoRunUnit, directory));
                return null;
            default:
                reporter.Report(ToolDiagnostic.Create(ToolDiagnostics.SeveralRunUnits, directory));
                return null;
        }
    }

    private static RunUnitKind? KindOf(string path)
    {
        var extension = Path.GetExtension(path.AsSpan());
        if (
            extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase)
        )
        {
            return RunUnitKind.Solution;
        }

        return extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase) ? RunUnitKind.Project : null;
    }
}
