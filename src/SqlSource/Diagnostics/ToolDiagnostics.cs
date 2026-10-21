using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace SqlSource.Diagnostics;

/// <summary>
/// The errors of the <c>sqlsource</c> tool that are not the generator's own; the tool reports some of
/// <see cref="SqlDiagnostics" /> too.  The generator reports two of these, where the tool reports them:
/// <see cref="OutputNeedsDescribableDialect" /> and <see cref="TokenHasNoDefault" />.  The others are here only so
/// that the release tracking and the tests of <c>docs/manual/diagnostics.md</c> cover them with the generator's own.
/// </summary>
/// <remarks>
/// Adding, removing or changing one also changes <c>AnalyzerReleases.Unshipped.md</c> and
/// <c>docs/manual/diagnostics.md</c>.
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
        messageFormat: "{0} holds no .sln, .slnx or .csproj file",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc201",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor SeveralRunUnits = new(
        id: "SQLSRC202",
        title: "More than one project or solution found",
        messageFormat: "{0} holds more than one .sln, .slnx or .csproj file.  Name the one to run on.",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc202",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor NotARunUnit = new(
        id: "SQLSRC203",
        title: "Path is not a project or a solution",
        messageFormat: "{0} is not a .sln, .slnx or .csproj file, or a directory that holds one",
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
        messageFormat: "{0} is not a project of '{1}'",
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
        messageFormat: "The output '{0}' needs each query described by its database, and the dialect '{1}' has no "
            + "database to ask.  Set the dialect to 'postgres' or 'mssql', or the output to 'sql'.",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc209",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor TokenHasNoDefault = new(
        id: "SQLSRC210",
        title: "Token has no default",
        messageFormat: "The token '{0}' has no default, which a query whose output is '{1}' needs.  Give it a default, "
            + "or set the output to 'sql'.",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc210",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor DatabaseHasTwoDialects = new(
        id: "SQLSRC211",
        title: "Database has two dialects",
        messageFormat: "The database '{0}' has the dialect '{1}' here and '{2}' in '{3}'",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc211",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor FileNotInRun = new(
        id: "SQLSRC212",
        title: "File is not in the run",
        messageFormat: "{0} is not a .sql file that a type of the run claims",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc212",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor DatabaseHasNoConnection = new(
        id: "SQLSRC213",
        title: "Database has no connection",
        messageFormat: "No connection is given for the database '{0}'",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc213",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor DatabasesShareConnectionVariable = new(
        id: "SQLSRC214",
        title: "Two databases share a connection variable",
        messageFormat: "The databases '{0}' and '{1}' both read their connection from {2}",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc214",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor UnnamedConnectionNotUsed = new(
        id: "SQLSRC215",
        title: "Connection names no database",
        messageFormat: "SQLSOURCE_CONNECTION is for a run with one database, and this run has {0}: {1}",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc215",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor NoDescriberForDialect = new(
        id: "SQLSRC216",
        title: "No describer for the dialect",
        messageFormat: "This version of sqlsource cannot describe '{0}'",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc216",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor SidecarNotWritten = new(
        id: "SQLSRC217",
        title: "Sidecar was not written",
        messageFormat: "The sidecar of '{0}' was not written, because not every query of the file has an entry",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc217",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor FileCannotBeChanged = new(
        id: "SQLSRC218",
        title: "File could not be changed",
        messageFormat: "'{0}' could not be {1}: {2}",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc218",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor SidecarOutOfDate = new(
        id: "SQLSRC219",
        title: "Sidecar is out of date",
        messageFormat: "The sidecar is out of date for '{0}': {1}",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc219",
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

    public static readonly DiagnosticDescriptor SidecarOfNewerTool = new(
        id: "SQLSRC221",
        title: "Sidecar was written by a newer tool",
        messageFormat: "'{0}' has format {1}, and this tool writes format {2}",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc221",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor DirectoryCannotBeRead = new(
        id: "SQLSRC222",
        title: "Directory cannot be read",
        messageFormat: "{0} cannot be read: {1}",
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

    public static readonly DiagnosticDescriptor DatabaseCannotBeOpened = new(
        id: "SQLSRC224",
        title: "Database could not be opened",
        messageFormat: "The database '{0}' could not be opened",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc224",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor ServerRejectedQuery = new(
        id: "SQLSRC225",
        title: "Server rejected the query",
        messageFormat: "The server rejected the query '{0}': {1}",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc225",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor ParameterTypeNotDetermined = new(
        id: "SQLSRC226",
        title: "Parameter type could not be determined",
        messageFormat: "The type of the parameter '{0}' of the query '{1}' could not be determined",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc226",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor ParameterFirstUseDoesNotFixType = new(
        id: "SQLSRC227",
        title: "Parameter's first use does not fix its type",
        messageFormat: "The first use of the parameter '{0}' of the query '{1}' does not fix its type",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc227",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor DeclaredTypeNotValid = new(
        id: "SQLSRC228",
        title: "Declared type is not valid",
        messageFormat: "The type '{0}' declared for the parameter '{1}' of the query '{2}' is not valid: {3}",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc228",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor ColumnTypeNotSupported = new(
        id: "SQLSRC229",
        title: "Column type is not supported",
        messageFormat: "The column '{0}' of the query '{1}' has the type '{2}', which cannot be described",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc229",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor QueryHasSeveralStatements = new(
        id: "SQLSRC230",
        title: "Query holds more than one statement",
        messageFormat: "The query '{0}' holds more than one statement",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc230",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor ParametersReadDifferently = new(
        id: "SQLSRC231",
        title: "Parameters are read differently",
        messageFormat: "{0} reads the parameters of the query '{1}' differently from SqlSource: {2}",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc231",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor TypeCannotBeBound = new(
        id: "SQLSRC232",
        title: "Type cannot be bound by Npgsql",
        messageFormat: "Npgsql cannot bind the type '{0}' of the parameter '{1}' of the query '{2}'",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc232",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor StatementCannotBeDescribed = new(
        id: "SQLSRC233",
        title: "Statement cannot be described",
        messageFormat: "The query '{0}' is a {1} statement, which the tool cannot describe",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc233",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor PositionalParameterWritten = new(
        id: "SQLSRC234",
        title: "Query writes a positional parameter",
        messageFormat: "The query '{0}' writes the parameter '{1}' itself",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc234",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor ServerVersionNotSupported = new(
        id: "SQLSRC235",
        title: "Server version is not supported",
        messageFormat: "The database '{0}' runs {1} {2}, and the tool supports {3} and later",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc235",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor SessionNotKept = new(
        id: "SQLSRC236",
        title: "Connection does not keep a session",
        messageFormat: "The connection to the database '{0}' does not keep one session",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc236",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor CopiesHaveDifferentTypes = new(
        id: "SQLSRC262",
        title: "Copies of a parameter have different types",
        messageFormat: "The copies of the parameter '{0}' of the query '{1}' have the types '{2}' and '{3}'",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc262",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor ConflictingResults = new(
        id: "SQLSRC263",
        title: "Query returns rows of different shapes",
        messageFormat: "The query '{0}' returns rows of different shapes on different paths",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc263",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor DynamicSql = new(
        id: "SQLSRC264",
        title: "Query runs SQL the server cannot read",
        messageFormat: "The query '{0}' runs SQL that the server cannot read until it runs",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc264",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor TemporaryTableInBatch = new(
        id: "SQLSRC265",
        title: "Query uses a temporary table in a batch",
        messageFormat: "The query '{0}' uses a temporary table in a batch of several statements",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc265",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor ColumnHasNoName = new(
        id: "SQLSRC266",
        title: "Column has no name",
        messageFormat: "Column {0} of the query '{1}' has no name",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc266",
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
            DatabaseHasTwoDialects,
            FileNotInRun,
            DatabaseHasNoConnection,
            DatabasesShareConnectionVariable,
            UnnamedConnectionNotUsed,
            NoDescriberForDialect,
            SidecarNotWritten,
            FileCannotBeChanged,
            SidecarOutOfDate,
            ProjectNotRestored,
            SidecarOfNewerTool,
            DirectoryCannotBeRead,
            SolutionCannotBeRead,
            DatabaseCannotBeOpened,
            ServerRejectedQuery,
            ParameterTypeNotDetermined,
            ParameterFirstUseDoesNotFixType,
            DeclaredTypeNotValid,
            ColumnTypeNotSupported,
            QueryHasSeveralStatements,
            ParametersReadDifferently,
            TypeCannotBeBound,
            StatementCannotBeDescribed,
            PositionalParameterWritten,
            ServerVersionNotSupported,
            SessionNotKept,
            CopiesHaveDifferentTypes,
            ConflictingResults,
            DynamicSql,
            TemporaryTableInBatch,
            ColumnHasNoName
        );
}
