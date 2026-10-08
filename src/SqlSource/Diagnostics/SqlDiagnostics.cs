using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using SqlSource.Parsing;

namespace SqlSource.Diagnostics;

/// <summary>
/// Every diagnostic the generator reports.  Each is an error that a consumer cannot turn off.
/// </summary>
/// <remarks>
/// Adding, removing or changing one also changes <c>AnalyzerReleases.Unshipped.md</c> and <c>docs/diagnostics.md</c>.
/// </remarks>
internal static class SqlDiagnostics
{
    private const string Category = "SqlSource";

    private const string HelpLinkBase = "https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#";

    public static readonly DiagnosticDescriptor TypeNotPartial = new(
        id: "SQLSRC001",
        title: "Type must be partial",
        messageFormat: "'{0}' must be declared partial so that SqlSource can add members to it",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc001",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor TypeIsFileLocal = new(
        id: "SQLSRC002",
        title: "Type is file-local",
        messageFormat: "'{0}' is a file-local type, and SqlSource cannot add members to it",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc002",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor UnsupportedTargetFramework = new(
        id: "SQLSRC003",
        title: "Target framework is not supported",
        messageFormat: "SqlSource generates code for .NET 8 and later, and this project targets an older framework",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc003",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor PathMatchesNothing = new(
        id: "SQLSRC004",
        title: "Path matches no SQL file",
        messageFormat: "Path '{0}' matches no .sql file.  It is relative to the folder of this file; a value that ends "
            + "in .sql is one file, and any other value is a folder.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc004",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor FolderHasNoSqlFile = new(
        id: "SQLSRC005",
        title: "Folder has no SQL file",
        messageFormat: "The folder of this file has no .sql file.  Add one, or set Path.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc005",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor InvalidSqlLocation = new(
        id: "SQLSRC006",
        title: "SqlLocation is not valid",
        messageFormat: "'{0}' is not a value of SqlLocation",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc006",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor SqlMemberExists = new(
        id: "SQLSRC007",
        title: "Type has a member named Sql",
        messageFormat: "'{0}' already has a member named 'Sql'.  Rename it, or use SqlLocation.Direct.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc007",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor DuplicateQueryName = new(
        id: "SQLSRC008",
        title: "Query name is used in two files",
        messageFormat: "The query name '{0}' is already used in '{1}', which also belongs to '{2}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc008",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor QueryNamedLikeContainingType = new(
        id: "SQLSRC009",
        title: "Query is named like its containing type",
        messageFormat: "A query of '{0}' cannot be named '{1}', because its member would have the name of the type "
            + "that contains it",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc009",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor InvalidTokenValidation = new(
        id: "SQLSRC010",
        title: "SqlSourceTokenValidation is not valid",
        messageFormat: "The MSBuild property SqlSourceTokenValidation is '{0}'.  It must be 'true' or 'false'.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc010",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor InvalidDialect = new(
        id: "SQLSRC011",
        title: "SqlSourceDialect is not valid",
        messageFormat: "'{0}' is not a SQL dialect.  SqlSourceDialect accepts ansi, mssql, postgres, cockroachdb, "
            + "mysql, mariadb, sqlite and oracle.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc011",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor UnsupportedLanguageVersion = new(
        id: "SQLSRC012",
        title: "Language version is not supported",
        messageFormat: "SqlSource generates C# 12 code, and this project's language version is {0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc012",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor PathDiffersOnlyByCase = new(
        id: "SQLSRC013",
        title: "SQL file paths differ only by case",
        messageFormat: "This file's path differs only by case from '{0}', and SqlSource compares paths ignoring case",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc013",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor UnterminatedQuote = new(
        id: "SQLSRC101",
        title: "Quote is not closed",
        messageFormat: "A quoted string or identifier is not closed",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc101",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor UnterminatedBlockComment = new(
        id: "SQLSRC102",
        title: "Comment is not closed",
        messageFormat: "A block comment or hint is not closed",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc102",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor InvalidName = new(
        id: "SQLSRC103",
        title: "Query name is not valid",
        messageFormat: "'{0}' is not a valid query name.  A name is a C# identifier that is not a reserved keyword.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc103",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor DuplicateName = new(
        id: "SQLSRC104",
        title: "Query name is used twice",
        messageFormat: "The query name '{0}' is used more than once in this file",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc104",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor InvalidFileName = new(
        id: "SQLSRC105",
        title: "File name is not a valid query name",
        messageFormat: "The file has no '-- name:' marker, and its name '{0}' does not give a valid query name.  Add a "
            + "marker or rename the file.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc105",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor SqlBeforeFirstName = new(
        id: "SQLSRC106",
        title: "SQL before the first name",
        messageFormat: "The SQL before the first '-- name:' marker belongs to no query",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc106",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor SummaryBeforeFirstName = new(
        id: "SQLSRC107",
        title: "Summary before the first name",
        messageFormat: "The '-- summary:' marker before the first '-- name:' marker belongs to no query",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc107",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor MarkerAtEndOfBlock = new(
        id: "SQLSRC108",
        title: "Marker has no SQL after it",
        messageFormat: "A '-- summary:' or '-- SqlSource:' marker comes before the SQL it describes, and no SQL "
            + "follows this one in its query",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc108",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor UnknownDirective = new(
        id: "SQLSRC109",
        title: "Directive is not known",
        messageFormat: "'{0}' is not a SqlSource directive",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc109",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor EmptyDirectiveLine = new(
        id: "SQLSRC110",
        title: "Directive is missing",
        messageFormat: "The '-- SqlSource:' marker has no directive",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc110",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor InvalidDirectiveValue = new(
        id: "SQLSRC111",
        title: "Directive value is not valid",
        messageFormat: "The directive '{0}' lacks a value it needs, has one it does not take, or has one that is not "
            + "valid",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc111",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor ConflictingDirectives = new(
        id: "SQLSRC112",
        title: "Directives conflict",
        messageFormat: "'{0}' conflicts with another directive in the same scope",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc112",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor EmptyBlock = new(
        id: "SQLSRC113",
        title: "Query has no SQL",
        messageFormat: "The query has no SQL",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc113",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor ReservedTokenName = new(
        id: "SQLSRC114",
        title: "Token name is a keyword",
        messageFormat: "The token name '{0}' is a reserved C# keyword",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc114",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor MisplacedDialect = new(
        id: "SQLSRC115",
        title: "Dialect directive is misplaced",
        messageFormat: "The 'dialect' directive must come before the file's first query and before any SQL",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc115",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    /// <summary>
    /// Every descriptor, in the order of its id.
    /// </summary>
    public static ImmutableArray<DiagnosticDescriptor> All { get; } =
        ImmutableArray.Create(
            TypeNotPartial,
            TypeIsFileLocal,
            UnsupportedTargetFramework,
            PathMatchesNothing,
            FolderHasNoSqlFile,
            InvalidSqlLocation,
            SqlMemberExists,
            DuplicateQueryName,
            QueryNamedLikeContainingType,
            InvalidTokenValidation,
            InvalidDialect,
            UnsupportedLanguageVersion,
            PathDiffersOnlyByCase,
            UnterminatedQuote,
            UnterminatedBlockComment,
            InvalidName,
            DuplicateName,
            InvalidFileName,
            SqlBeforeFirstName,
            SummaryBeforeFirstName,
            MarkerAtEndOfBlock,
            UnknownDirective,
            EmptyDirectiveLine,
            InvalidDirectiveValue,
            ConflictingDirectives,
            EmptyBlock,
            ReservedTokenName,
            MisplacedDialect
        );

    /// <summary>
    /// The descriptor for a problem the parser found.
    /// </summary>
    public static DiagnosticDescriptor ForParseError(SqlParseErrorKind kind) =>
        kind switch
        {
            SqlParseErrorKind.UnterminatedQuote => UnterminatedQuote,
            SqlParseErrorKind.UnterminatedBlockComment => UnterminatedBlockComment,
            SqlParseErrorKind.InvalidName => InvalidName,
            SqlParseErrorKind.DuplicateName => DuplicateName,
            SqlParseErrorKind.InvalidFileName => InvalidFileName,
            SqlParseErrorKind.SqlBeforeFirstName => SqlBeforeFirstName,
            SqlParseErrorKind.SummaryBeforeFirstName => SummaryBeforeFirstName,
            SqlParseErrorKind.MarkerAtEndOfBlock => MarkerAtEndOfBlock,
            SqlParseErrorKind.UnknownDirective => UnknownDirective,
            SqlParseErrorKind.EmptyDirectiveLine => EmptyDirectiveLine,
            SqlParseErrorKind.InvalidDirectiveValue => InvalidDirectiveValue,
            SqlParseErrorKind.ConflictingDirectives => ConflictingDirectives,
            SqlParseErrorKind.EmptyBlock => EmptyBlock,
            SqlParseErrorKind.ReservedTokenName => ReservedTokenName,
            SqlParseErrorKind.MisplacedDialect => MisplacedDialect,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No descriptor is defined for this kind."),
        };
}
