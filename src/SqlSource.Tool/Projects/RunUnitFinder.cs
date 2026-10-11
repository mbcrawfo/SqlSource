using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using SqlSource.Diagnostics;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Projects;

/// <summary>
/// Finds the unit of a run from the path on the command line, the way <c>dotnet build</c> finds what to build.
/// </summary>
internal static class RunUnitFinder
{
    /// <summary>
    /// The unit, or the error that says why there is none.
    /// </summary>
    /// <param name="argument">The path as given, or null when none was.</param>
    /// <param name="workingDirectory">The full path a relative path is resolved against.</param>
    public static Outcome<RunUnit, ToolDiagnostic> Find(string? argument, string workingDirectory)
    {
        // Path.GetFullPath throws for an empty path, which is what an unset variable of a shell gives.
        if (argument is { Length: 0 })
        {
            return No(ToolDiagnostics.NotARunUnit, argument);
        }

        var path = Path.TrimEndingDirectorySeparator(
            argument is null ? workingDirectory : Path.GetFullPath(argument, workingDirectory)
        );

        if (Directory.Exists(path))
        {
            return FindIn(path);
        }

        if (File.Exists(path) && KindOf(path) is { } kind)
        {
            return Outcome<RunUnit, ToolDiagnostic>.Of(new RunUnit(kind, path));
        }

        return No(ToolDiagnostics.NotARunUnit, path);
    }

    private static Outcome<RunUnit, ToolDiagnostic> FindIn(string directory)
    {
        (string File, RunUnitKind? Kind)[] units;
        try
        {
            // Only files: a folder named App.csproj is not a project.
            units =
            [
                .. Directory
                    .EnumerateFiles(directory)
                    .Select(static file => (File: file, Kind: KindOf(file)))
                    .Where(static unit => unit.Kind is not null)
                    .Take(2),
            ];
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            // The user named this directory, so that it cannot be read is theirs to mend, and no bug of the tool.
            return No(ToolDiagnostics.DirectoryCannotBeRead, directory, exception.Message);
        }

        return units switch
        {
            [{ Kind: { } kind } unit] => Outcome<RunUnit, ToolDiagnostic>.Of(new RunUnit(kind, unit.File)),
            [] => No(ToolDiagnostics.NoRunUnit, directory),
            _ => No(ToolDiagnostics.SeveralRunUnits, directory),
        };
    }

    private static Outcome<RunUnit, ToolDiagnostic> No(DiagnosticDescriptor descriptor, params string[] arguments) =>
        Outcome<RunUnit, ToolDiagnostic>.Failed(ToolDiagnostic.Create(descriptor, arguments));

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
