using System;
using System.Collections.Immutable;
using SqlSource.Diagnostics;
using SqlSource.Generation.Chains;
using SqlSource.Generation.Paths;
using SqlSource.Settings;

namespace SqlSource.Generation;

/// <summary>
/// Names a type's nested class, from its attribute, the chain of the folder of its C# file and the project's property,
/// and reports <c>SQLSRC007</c> when the type already uses the name.
/// </summary>
internal static class NestedClassCheck
{
    /// <summary>
    /// The type with its nested class named.  The names it cannot take are left behind, so that an edit that adds a
    /// member reaches no step after this one unless it takes the name.
    /// </summary>
    /// <param name="declared">The type as it was read.</param>
    /// <param name="settingsFiles">
    /// The project's settings files, among which the chain of the folder of the type's C# file is searched for.
    /// </param>
    /// <param name="property">What the project's properties say.</param>
    public static TargetType Apply(DeclaredType declared, SettingsFileSet settingsFiles, SettingsLevel property)
    {
        var type = declared.Type;
        if (type.Placement != MemberPlacement.Nested)
        {
            return type;
        }

        // The path is normalised only for a type whose members are nested, and only when the project has a settings
        // file.
        var chain =
            settingsFiles.Files.Count > 0 && SqlPath.Normalize(type.FilePath) is { } path
                ? settingsFiles.ChainOf(path)
                : SettingsChain.None;
        var name = QuerySettings.ResolveNestedClass(type.Settings, chain.Level, property);
        if (SortedSearch.IndexOf(declared.TakenNames, name, static taken => taken, StringComparer.Ordinal) < 0)
        {
            return name == type.NestedClass ? type : type with { NestedClass = name };
        }

        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>(type.Diagnostics.Count + 1);
        diagnostics.AddRange(type.Diagnostics);
        diagnostics.Add(
            DiagnosticInfo.Create(SqlDiagnostics.NestedClassNameTaken, type.AttributeLocation, type.Name, name)
        );
        return type with
        {
            NestedClass = name,
            Diagnostics = new EquatableArray<DiagnosticInfo>(diagnostics.MoveToImmutable()),
        };
    }
}
