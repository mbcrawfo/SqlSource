using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace SqlSource.Diagnostics;

/// <summary>
/// Every error that only the <c>sqlsource</c> tool reports.  The generator reports none of them: they are here so
/// that the release tracking and the tests of <c>docs/diagnostics.md</c> cover them with the generator's own.
/// </summary>
/// <remarks>
/// Adding, removing or changing one also changes <c>AnalyzerReleases.Unshipped.md</c> and <c>docs/diagnostics.md</c>.
/// </remarks>
internal static class ToolDiagnostics
{
    public static readonly DiagnosticDescriptor UnexpectedFailure = new(
        id: "SQLSRC200",
        title: "The tool failed unexpectedly",
        messageFormat: "sqlsource failed unexpectedly: {0}: {1}",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc200",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor NoRunUnit = new(
        id: "SQLSRC201",
        title: "No project or solution found",
        messageFormat: "'{0}' holds no .sln, .slnx or .csproj file",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc201",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor SeveralRunUnits = new(
        id: "SQLSRC202",
        title: "More than one project or solution found",
        messageFormat: "'{0}' holds more than one .sln, .slnx or .csproj file.  Name the one to run on.",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc202",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor NotARunUnit = new(
        id: "SQLSRC203",
        title: "Path is not a project or a solution",
        messageFormat: "'{0}' is not a .sln, .slnx or .csproj file, or a directory that holds one",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc203",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    /// <summary>
    /// Every descriptor, in the order of its id.
    /// </summary>
    public static ImmutableArray<DiagnosticDescriptor> All { get; } =
        ImmutableArray.Create(UnexpectedFailure, NoRunUnit, SeveralRunUnits, NotARunUnit);
}
