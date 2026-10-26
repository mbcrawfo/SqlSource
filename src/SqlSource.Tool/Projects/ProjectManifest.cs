using System.Collections.Immutable;

namespace SqlSource.Tool.Projects;

/// <summary>
/// What the compiler is given for one project, as the target <c>SqlSourceWriteManifest</c> of the package wrote it.
/// </summary>
/// <remarks>
/// A class and not a record: its collections would compare by reference, and nothing compares two manifests.
/// </remarks>
internal sealed class ProjectManifest
{
    /// <summary>The full path of the project file.</summary>
    public required string ProjectPath { get; init; }

    /// <summary>The framework the manifest was written for.  Empty for a project that is not of the SDK.</summary>
    public required string TargetFramework { get; init; }

    /// <summary>The project's <c>LangVersion</c> as MSBuild has it: empty, a number, or a word as latest is.</summary>
    public required string LangVersion { get; init; }

    /// <summary>The constants the project's code is compiled with.</summary>
    public required ImmutableArray<string> DefineConstants { get; init; }

    /// <summary>The properties of the package that have a value, by name, trimmed.</summary>
    public required ImmutableDictionary<string, string> Properties { get; init; }

    /// <summary>The <c>.sql</c> files of the project, in the order of its items.</summary>
    public required ImmutableArray<ManifestFile> Files { get; init; }

    /// <summary>
    /// The full paths of the settings files the compiler is given, each <c>sqlsource.json</c> above a <c>.sql</c> file
    /// or a <c>Compile</c> item and any the project lists itself, once each, in the order of the manifest.
    /// </summary>
    public required ImmutableArray<string> SettingsFiles { get; init; }

    /// <summary>The full paths of the project's <c>Compile</c> items.</summary>
    public required ImmutableArray<string> CompileFiles { get; init; }
}
