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

    public static readonly DiagnosticDescriptor ProjectDoesNotUseSqlSource = new(
        id: "SQLSRC204",
        title: "Project does not use SqlSource",
        messageFormat: "'{0}' does not use SqlSource",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc204",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor ProjectCannotBeEvaluated = new(
        id: "SQLSRC205",
        title: "Project could not be evaluated",
        messageFormat: "MSBuild could not evaluate '{0}'",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc205",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor ManifestCannotBeRead = new(
        id: "SQLSRC206",
        title: "Project manifest cannot be read",
        messageFormat: "The project manifest of '{0}' cannot be read: {1}",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc206",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor ProjectNotInRun = new(
        id: "SQLSRC207",
        title: "Project is not in the run",
        messageFormat: "'{0}' is not a project of '{1}'",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc207",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor AttributeArgumentNotLiteral = new(
        id: "SQLSRC208",
        title: "Attribute argument is not a literal",
        messageFormat: "'{0}' of [SqlSourceGenerate] is read from the source by 'sqlsource', which needs a literal "
            + "here",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc208",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor OutputNeedsDescribableDialect = new(
        id: "SQLSRC209",
        title: "Output needs a dialect that can be described",
        messageFormat: "The output '{0}' needs a dialect that can be described, and the dialect of this file is "
            + "'{1}'.  Set the dialect to 'postgres' or 'mssql', or the output to 'sql'.",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc209",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor TokenHasNoDefault = new(
        id: "SQLSRC210",
        title: "Token has no default",
        messageFormat: "The token '{0}' has no default.  A query whose output is '{1}' is described with a sample in "
            + "its place.",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc210",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor ProjectNotRestored = new(
        id: "SQLSRC220",
        title: "Project was not restored",
        messageFormat: "'{0}' has not been restored, or not since the SqlSource package was added to it",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc220",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor DirectoryCannotBeRead = new(
        id: "SQLSRC222",
        title: "Directory cannot be read",
        messageFormat: "'{0}' cannot be read: {1}",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc222",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor SolutionCannotBeRead = new(
        id: "SQLSRC223",
        title: "Solution cannot be read",
        messageFormat: "'{0}' cannot be read: {1}",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc223",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    /// <summary>
    /// Every descriptor, in the order of its id.
    /// </summary>
    public static ImmutableArray<DiagnosticDescriptor> All { get; } =
        ImmutableArray.Create(
            UnexpectedFailure,
            NoRunUnit,
            SeveralRunUnits,
            NotARunUnit,
            ProjectDoesNotUseSqlSource,
            ProjectCannotBeEvaluated,
            ManifestCannotBeRead,
            ProjectNotInRun,
            AttributeArgumentNotLiteral,
            OutputNeedsDescribableDialect,
            TokenHasNoDefault,
            ProjectNotRestored,
            DirectoryCannotBeRead,
            SolutionCannotBeRead
        );
}
