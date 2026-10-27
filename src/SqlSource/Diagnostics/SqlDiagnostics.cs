using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using SqlSource.Parsing;

namespace SqlSource.Diagnostics;

/// <summary>
/// The generator's own diagnostics: every one it reports but <c>SQLSRC209</c> and <c>SQLSRC210</c>, which are in
/// <see cref="ToolDiagnostics" />.  Each is an error that a consumer cannot turn off, unless its descriptor
/// says why a project may configure it, as a warning or as information; <c>SqlDiagnosticsTests</c> lists those by id.
/// </summary>
/// <remarks>
/// Adding, removing or changing one also changes <c>AnalyzerReleases.Unshipped.md</c> and
/// <c>docs/manual/diagnostics.md</c>.
/// </remarks>
internal static class SqlDiagnostics
{
    internal const string Category = "SqlSource";

    internal const string HelpLinkBase = "https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/diagnostics.md#";

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

    public static readonly DiagnosticDescriptor InvalidAttributeValue = new(
        id: "SQLSRC006",
        title: "Attribute value is not valid",
        messageFormat: "'{0}' is not a valid value of {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc006",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor NestedClassNameTaken = new(
        id: "SQLSRC007",
        title: "Type has a member named like its nested class",
        messageFormat: "'{0}' already has a member named '{1}'.  Rename it, or use SqlLocation.Direct.",
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

    public static readonly DiagnosticDescriptor SettingsFileNotAnObject = new(
        id: "SQLSRC015",
        title: "Settings file is not a JSON object",
        messageFormat: "This settings file is not one JSON object: {0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc015",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor SettingsFileUnknownKey = new(
        id: "SQLSRC016",
        title: "Settings file key is not known",
        messageFormat: "'{0}' is not a key of a settings file",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc016",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor SettingsFileDuplicateKey = new(
        id: "SQLSRC017",
        title: "Settings file key is written twice",
        messageFormat: "'{0}' is written twice in this settings file, and the first applies",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc017",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor SettingsFileWrongType = new(
        id: "SQLSRC018",
        title: "Settings file value has the wrong type",
        messageFormat: "'{0}' takes {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc018",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor SettingsFileInvalidValue = new(
        id: "SQLSRC019",
        title: "Settings file value is not valid",
        messageFormat: "'{0}' is not a valid value of {1}{2}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc019",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor SettingsFileRepeatedKey = new(
        id: "SQLSRC020",
        title: "Settings file entry repeats a key",
        messageFormat: "'{0}' is the same key as '{1}' in {2}, and the first applies",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc020",
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
        messageFormat: "A marker comes before the SQL it describes, and no SQL follows this one in its query",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc108",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor UnknownGeneratorParameter = new(
        id: "SQLSRC109",
        title: "Generator parameter is not known",
        messageFormat: "'{0}' is not a generator parameter",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc109",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor EmptyGeneratorLine = new(
        id: "SQLSRC110",
        title: "Generator parameter is missing",
        messageFormat: "The '-- generator:' marker has no parameter",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc110",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor InvalidMarkerValue = new(
        id: "SQLSRC111",
        title: "Marker value is not valid",
        messageFormat: "'{0}' lacks a value it needs, has one it does not take, or has one that is not valid{1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc111",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor ConflictingSettings = new(
        id: "SQLSRC112",
        title: "Settings conflict",
        messageFormat: "'{0}' conflicts with a setting given earlier in the same scope",
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
        title: "Dialect marker is misplaced",
        messageFormat: "The '-- dialect:' marker must come before the file's first query and before any SQL",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc115",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor MarkerNotAllowedHere = new(
        id: "SQLSRC116",
        title: "Marker is not allowed here",
        messageFormat: "The '-- {0}:' marker is allowed only {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc116",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor MissingParameterType = new(
        id: "SQLSRC117",
        title: "Parameter has no type",
        messageFormat: "'{0}' is not in the SQL of its query, so its '-- param:' marker must give its type",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc117",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor UndeclaredParameter = new(
        id: "SQLSRC118",
        title: "Parameter is not declared",
        messageFormat: "'{0}' appears only in the default of a token.  Declare it with a '-- param:' marker that "
            + "gives its type.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc118",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor InputModelWithoutParameters = new(
        id: "SQLSRC119",
        title: "Query has no parameters",
        messageFormat: "The query has no parameters, so '-- input-model:' names nothing",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc119",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor TokenCrossesComment = new(
        id: "SQLSRC120",
        title: "Token crosses a comment",
        messageFormat: "Token '{0}' and a comment overlap, so the token is read differently where comments are kept.  "
            + "Keep a comment wholly inside the token's default, or outside the token.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc120",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor SidecarMalformed = new(
        id: "SQLSRC237",
        title: "Sidecar cannot be read",
        messageFormat: "The sidecar cannot be read, because {0}.  Run 'sqlsource describe' to write it again.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc237",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor SidecarFormatTooNew = new(
        id: "SQLSRC238",
        title: "Sidecar has a newer format",
        messageFormat: "The sidecar has format {0}, and this version of SqlSource reads format {1}.  Update the "
            + "SqlSource package.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc238",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor SidecarFormatTooOld = new(
        id: "SQLSRC239",
        title: "Sidecar has an older format",
        messageFormat: "The sidecar has format {0}, and this version of SqlSource reads format {1}.  Run "
            + "'sqlsource describe' to write it again.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc239",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    // A warning a project may configure: a sidecar of another version of the tool is still read and used, since the
    // format is the same.  An error would break every build after the package is updated until the tool runs.  A
    // project that commits its sidecars and treats warnings as errors may list it in WarningsNotAsErrors.
    public static readonly DiagnosticDescriptor SidecarToolVersionDiffers = new(
        id: "SQLSRC240",
        title: "Sidecar was written by another version of the tool",
        messageFormat: "The sidecar was written by sqlsource {0}, and this project uses SqlSource {1}.  Bring the tool "
            + "and the package to one version, and run 'sqlsource describe'.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc240"
    );

    public static readonly DiagnosticDescriptor QueryHasNoEntry = new(
        id: "SQLSRC241",
        title: "Query has no entry in the sidecar",
        messageFormat: "The query '{0}' has no entry in the sidecar of its file, and its output '{1}' needs one.  "
            + "Run 'sqlsource describe'.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc241",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor EntryEngineDiffers = new(
        id: "SQLSRC242",
        title: "Entry is of another database",
        messageFormat: "The entry of the query '{0}' was described for '{1}', and the dialect of this file is "
            + "'{2}'.  Run 'sqlsource describe'.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc242",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor EngineHasNoTypeMap = new(
        id: "SQLSRC243",
        title: "No type map for the dialect",
        messageFormat: "This version of SqlSource has no type map for the dialect '{0}', so no query of this file "
            + "can have models.  Set the output to 'sql'.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc243",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor EntryIsStale = new(
        id: "SQLSRC244",
        title: "Entry is stale",
        messageFormat: "The query '{0}' has changed since its entry was written.  Run 'sqlsource describe'.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc244",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    // A warning a project may configure: an entry that nothing reads generates nothing wrong.  An error would break
    // every build after a query is removed, or set to the output sql, until the tool runs.
    public static readonly DiagnosticDescriptor OrphanEntry = new(
        id: "SQLSRC245",
        title: "Entry names no query",
        messageFormat: "The entry '{0}' names no query of the file that needs one.  Run 'sqlsource describe', which "
            + "removes it.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc245"
    );

    // {0} is the column or parameter, as "column 'id'", {1} its type as the database spells it, and {2} the help,
    // without its period: the cast for a type Npgsql reads once it is cast, 'sqlsource describe' for a type that only
    // a sidecar edited by hand or written by a newer tool has, and otherwise the type override's entry for its key.
    public static readonly DiagnosticDescriptor TypeHasNoCSharpType = new(
        id: "SQLSRC246",
        title: "Type has no C# type",
        messageFormat: "The {0} has the type '{1}', which SqlSource cannot give a C# type.  {2}.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc246",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor NpgsqlTooOld = new(
        id: "SQLSRC247",
        title: "Npgsql is older than 8.0",
        messageFormat: "This project references Npgsql {0}, and SqlSource needs Npgsql 8.0 or later, so no query "
            + "method is generated.  Update the package.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc247",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    // Information a project may configure, not a warning: the models of a newer Npgsql use the types of Npgsql 10,
    // which are right unless the newer driver changed one, and a warning would break the build of every project that
    // treats warnings as errors on the day it updates the driver.
    public static readonly DiagnosticDescriptor NpgsqlNewerThanKnown = new(
        id: "SQLSRC248",
        title: "Npgsql is newer than SqlSource knows",
        messageFormat: "This project references Npgsql {0}, which is newer than this version of SqlSource knows.  Its "
            + "models use the types of Npgsql 10.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc248"
    );

    // {0} is the column or parameter, as "column 'id'", {1} its type as the database spells it, and {2} the help,
    // without its period: to reference Npgsql, and for a type whose C# type differs between the drivers, the type
    // override's entry for each.
    public static readonly DiagnosticDescriptor TypeNeedsNpgsql = new(
        id: "SQLSRC249",
        title: "Type needs Npgsql",
        messageFormat: "The {0} has the type '{1}', whose C# type depends on Npgsql, and this project does not "
            + "reference it.  {2}.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc249",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    // {0} is the column or parameter, as "column 'id'", {1} what is mapped, the type override's key or a parameter
    // as "@author", {2} the C# type, {3} the place that chose it, as "the property SqlSourceTypeOverrides" or "the
    // -- param: marker of the parameter", and {4} the types a short name in it could mean, as ", and it could be
    // App.UserId or Other.UserId", or empty.
    public static readonly DiagnosticDescriptor MappedTypeNotFound = new(
        id: "SQLSRC250",
        title: "Mapped type is not found",
        messageFormat: "'{1}' is mapped to '{2}' for {0}, in {3}, and no type of that name is found, or "
            + "more than one that this project can see{4}.  Correct the name, with its namespace and type arguments, "
            + "or for a name that two references have, map to a type of this project.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc250",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    // {0} to {3} as SQLSRC250's, {4} the case, a type this project cannot see or a type that is not public in a model
    // that is, and {5} its help, without its period.
    public static readonly DiagnosticDescriptor MappedTypeNotAccessible = new(
        id: "SQLSRC251",
        title: "Mapped type is not accessible",
        messageFormat: "'{1}' is mapped to '{2}' for {0}, in {3}, and {4}.  {5}.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc251",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor MappedTypeNotAPropertyType = new(
        id: "SQLSRC252",
        title: "Mapped type cannot be a property's type",
        messageFormat: "'{1}' is mapped to '{2}' for {0}, in {3}, and no property can have that type.  "
            + "Choose another type, and for System.Nullable<T> name T, since a column that can be null makes its "
            + "property nullable.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc252",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    // {0} is the column's position, 1-based, and {1} the query's name.
    public static readonly DiagnosticDescriptor ColumnHasNoName = new(
        id: "SQLSRC253",
        title: "Column has no name",
        messageFormat: "The column at position {0} of the query '{1}' has no name.  Give it an alias.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc253",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    // {0} is the column or parameter, as "column 'id'", {1} the property name it gives, and {2} the help, without its
    // period: an alias for a column, a new name for a parameter.
    public static readonly DiagnosticDescriptor PropertyNameNotIdentifier = new(
        id: "SQLSRC254",
        title: "Name is not an identifier after PascalCase",
        messageFormat: "The {0} gives the property name '{1}', which is not a C# identifier.  {2}.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc254",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    // {0} and {1} are the two columns or parameters, as "column 'id' at position 1", {2} the property name of the
    // first, and {3} the help, without its period.
    public static readonly DiagnosticDescriptor PropertyNameRepeated = new(
        id: "SQLSRC255",
        title: "Two names give one property name",
        messageFormat: "The {0} and the {1} give one property name, '{2}', ignoring case.  {3}.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc255",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    // {0} is the column or parameter, as "column 'id'", {1} the property name it gives, {2} what takes it, as "a
    // record or object already has", and {3} the help, without its period.
    public static readonly DiagnosticDescriptor PropertyNameTaken = new(
        id: "SQLSRC256",
        title: "Property name is taken",
        messageFormat: "The {0} gives the property name '{1}', which {2}.  {3}.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc256",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    // {0} and {3} are the roles of the later and the earlier model, "input" or "output", {1} the full name, {2} the
    // later query's name, {4} the earlier query, as "'GetUser' in Users.sql", with ", for the type App.Repo" when the
    // two are one query, and {5} the first difference.
    public static readonly DiagnosticDescriptor ModelsDiffer = new(
        id: "SQLSRC257",
        title: "Two models of one name differ",
        messageFormat: "The {0} model '{1}' of the query '{2}' is not the same as the {3} model of that name of the "
            + "query {4}: {5}.  Give one of the two queries another model name, or make the two the same.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc257",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    // {0} is the model's full name, {1} the query's name and {2} why the name cannot be used.
    public static readonly DiagnosticDescriptor ModelNameUnusable = new(
        id: "SQLSRC258",
        title: "Model name cannot be used",
        messageFormat: "The model '{0}' of the query '{1}' cannot be generated: {2}.  Give the query another model "
            + "name, or set another model namespace.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc258",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor OutputModelWithoutRows = new(
        id: "SQLSRC259",
        title: "Output model on a query without rows",
        messageFormat: "The query '{0}' names an output model, and its statement returns no rows.  Remove the "
            + "-- output-model: marker.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc259",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    // {0} is the column's name as the marker gives it, {1} what is wrong with it, as "names no column of the query's
    // result", and {2} the help, without its period.
    public static readonly DiagnosticDescriptor ColumnMarkerUnusable = new(
        id: "SQLSRC260",
        title: "Column marker names no usable column",
        messageFormat: "The -- column: marker for '{0}' {1}.  {2}.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc260",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    // {0} is the column or parameter, as "column 'id'", {1} the entry's key as written, {2} the place that wrote it,
    // as "the property SqlSourceTypeOverrides", and {3} the key to write, the element's with [].
    public static readonly DiagnosticDescriptor ArrayEntryMapsNothing = new(
        id: "SQLSRC261",
        title: "Entry names an array as the catalog does",
        messageFormat: "The type override's entry for '{1}', in {2}, maps nothing: '{1}' is the database's own name of "
            + "the array type of {0}, and an entry names that array as its element's key with [].  Write the entry "
            + "for '{3}' instead.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc261",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor SqlClientTooOld = new(
        id: "SQLSRC267",
        title: "Microsoft.Data.SqlClient is older than 6.1",
        messageFormat: "This project references Microsoft.Data.SqlClient {0}, and SqlSource needs "
            + "Microsoft.Data.SqlClient 6.1 or later, so no query method is generated.  Update the package.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc267",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    // Information a project may configure, not a warning, for the reason of SQLSRC248: the models of a newer
    // Microsoft.Data.SqlClient use the types of Microsoft.Data.SqlClient 7, which are right unless the newer driver
    // changed one, and a warning would break the build of every project that treats warnings as errors on the day it
    // updates the driver.
    public static readonly DiagnosticDescriptor SqlClientNewerThanKnown = new(
        id: "SQLSRC268",
        title: "Microsoft.Data.SqlClient is newer than SqlSource knows",
        messageFormat: "This project references Microsoft.Data.SqlClient {0}, which is newer than this version of "
            + "SqlSource knows.  Its models use the types of Microsoft.Data.SqlClient 7.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc268"
    );

    // {0} is the column or parameter, as "column 'id'", {1} its type as the database spells it, and {2} the help,
    // without its period: to reference Microsoft.Data.SqlClient.
    public static readonly DiagnosticDescriptor TypeNeedsSqlClient = new(
        id: "SQLSRC269",
        title: "Type needs Microsoft.Data.SqlClient",
        messageFormat: "The {0} has the type '{1}', whose C# type depends on Microsoft.Data.SqlClient, and this "
            + "project does not reference it.  {2}.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc269",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    // {0} is the column or parameter, as "column 'id'", {1} its type as the database spells it, {2} the version of
    // Microsoft.Data.SqlClient that first has its C# type, and {3} the version the project references, as
    // SqlClientProbe.VersionText gives it: "6.0.5", or "of an unknown version".
    public static readonly DiagnosticDescriptor TypeNeedsNewerSqlClient = new(
        id: "SQLSRC270",
        title: "Type needs a newer Microsoft.Data.SqlClient",
        messageFormat: "The {0} has the type '{1}', whose C# type needs Microsoft.Data.SqlClient {2} or later, and "
            + "this project references Microsoft.Data.SqlClient {3}.  Update the package.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc270",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    // {0} is the query, {1} its shape as the name marker writes it: one, one-optional or many.
    public static readonly DiagnosticDescriptor QueryShapeNeedsRows = new(
        id: "SQLSRC272",
        title: "Result shape needs rows",
        messageFormat: "The query '{0}' has the shape '{1}', and its statement returns no rows.  Remove the shape, or "
            + "write 'rowcount' or 'none'.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc272",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    // {0} is the database, PostgreSQL or SQL Server, and {1} its driver's package.  Reported with Location.None, once
    // for the project and engine.
    public static readonly DiagnosticDescriptor DriverNotReferenced = new(
        id: "SQLSRC273",
        title: "Driver is not referenced",
        messageFormat: "A query for {0} has the output codegen, and this project does not reference {1}, which its "
            + "method needs.  Reference {1}, or set the output to models.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc273",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    // {0} is the query, {1} the name its method would have, and {2} what takes the name, as "'Sample' has a member of
    // that name".
    public static readonly DiagnosticDescriptor MethodNameTaken = new(
        id: "SQLSRC274",
        title: "Method name is taken",
        messageFormat: "The method of the query '{0}' cannot be named '{1}', because {2}.  Rename the query.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc274",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    // {0} is the token's name and {1} the query.
    public static readonly DiagnosticDescriptor TokenNamedAsAnArgument = new(
        id: "SQLSRC275",
        title: "Token is named as an argument of the method",
        messageFormat: "The token '{0}' of the query '{1}' has the name of an argument that every query method has: "
            + "connection, parameters, transaction, commandTimeout or cancellationToken.  Rename the token.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc275",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    // {0} is the attributed type in full, {1} the extension class's full name, and {2} what stops the class, as "the
    // project has a type of that name".  Reported at the type's attribute.
    public static readonly DiagnosticDescriptor ExtensionClassNameTaken = new(
        id: "SQLSRC276",
        title: "Extension class name is taken",
        messageFormat: "The query methods of '{0}' cannot go into the extension class '{1}', because {2}.  Rename the "
            + "type or what has the name, or set the attribute's MethodLocation to put the methods on the type.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc276",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    // {0} is the method as "App.Email.FromDatabase(string)", {1} its first fault, as "it is not static".  Reported at
    // the method's name for a method of the project's source, and at the query's name for one of a referenced
    // assembly that the query's method would use.
    public static readonly DiagnosticDescriptor NotAConverter = new(
        id: "SQLSRC277",
        title: "Method marked as a converter is not one",
        messageFormat: "The method '{0}' is marked [SqlSourceConverter], but {1}.  A converter is a static method of "
            + "one argument that returns the converted value.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc277",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    // {0} and {1} are the two methods as "App.Email.FromDatabase(string)", {2} the type of their argument and {3} their
    // return type, as PostgresTypeMap.AsWritten writes them.  Reported at the name of each method of the project's
    // source, and at the query's name for two of a referenced assembly that the query's method would use.
    public static readonly DiagnosticDescriptor DuplicateConverter = new(
        id: "SQLSRC278",
        title: "Two converters convert between the same types",
        messageFormat: "The converters '{0}' and '{1}' both convert '{2}' to '{3}'.  Keep one.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc278",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    // It has two forms, which ConversionPlanner builds.  {0} is the column or parameter, as "column 'id'", or "element
    // of column 'ids'" when the foreign type is an array's element, and {1} its type, or the element's, as
    // PostgresTypeMap.AsWritten writes it.  For a foreign type whose converters are of other types, {2} is "whose
    // converters convert from and to <the other types, as 'int' or 'int' and 'short'>, and not from and to '<the
    // base map's type>', the type the database type '<its key>' is read as", and {3} "Add a converter between '<the
    // type>' and '<the base map's type>'".  For a database type the base map gives no C# type, {2} is "which has
    // converters, but the database type '<its key>' has no C# type to convert from", and {3} "Remove the type
    // override, or the converters of '<the type of {1}>'".  Reported at the query's name.
    public static readonly DiagnosticDescriptor ConverterTypeMismatch = new(
        id: "SQLSRC279",
        title: "Converters do not convert from the database type",
        messageFormat: "The {0} has type '{1}', {2}.  {3}.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc279",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    // {0} is the column or parameter, as "column 'email'", or "element of column 'emails'" for an array converted
    // element by element, {1} its type or the element's, {2} "to" or "from", the direction of the converter it has,
    // {3} the type the base map reads its database type as, {4} the other of "to" and "from", {5} "column" or
    // "parameter", and {6} and {7} the argument and return types of the converter it needs.  The types are as
    // PostgresTypeMap.AsWritten writes them.  Reported at the query's name.
    public static readonly DiagnosticDescriptor ConverterDirectionMissing = new(
        id: "SQLSRC280",
        title: "Converter is missing for one direction",
        messageFormat: "The {0} has type '{1}', which has a converter {2} '{3}' but none {4} it, which a {5} needs.  "
            + "Add a static method marked [SqlSourceConverter] that takes '{6}' and returns '{7}'.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc280",
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
            InvalidAttributeValue,
            NestedClassNameTaken,
            DuplicateQueryName,
            QueryNamedLikeContainingType,
            UnsupportedLanguageVersion,
            PathDiffersOnlyByCase,
            SettingsFileNotAnObject,
            SettingsFileUnknownKey,
            SettingsFileDuplicateKey,
            SettingsFileWrongType,
            SettingsFileInvalidValue,
            SettingsFileRepeatedKey,
            UnterminatedQuote,
            UnterminatedBlockComment,
            InvalidName,
            DuplicateName,
            InvalidFileName,
            SqlBeforeFirstName,
            SummaryBeforeFirstName,
            MarkerAtEndOfBlock,
            UnknownGeneratorParameter,
            EmptyGeneratorLine,
            InvalidMarkerValue,
            ConflictingSettings,
            EmptyBlock,
            ReservedTokenName,
            MisplacedDialect,
            MarkerNotAllowedHere,
            MissingParameterType,
            UndeclaredParameter,
            InputModelWithoutParameters,
            TokenCrossesComment,
            SidecarMalformed,
            SidecarFormatTooNew,
            SidecarFormatTooOld,
            SidecarToolVersionDiffers,
            QueryHasNoEntry,
            EntryEngineDiffers,
            EngineHasNoTypeMap,
            EntryIsStale,
            OrphanEntry,
            TypeHasNoCSharpType,
            NpgsqlTooOld,
            NpgsqlNewerThanKnown,
            TypeNeedsNpgsql,
            MappedTypeNotFound,
            MappedTypeNotAccessible,
            MappedTypeNotAPropertyType,
            ColumnHasNoName,
            PropertyNameNotIdentifier,
            PropertyNameRepeated,
            PropertyNameTaken,
            ModelsDiffer,
            ModelNameUnusable,
            OutputModelWithoutRows,
            ColumnMarkerUnusable,
            ArrayEntryMapsNothing,
            SqlClientTooOld,
            SqlClientNewerThanKnown,
            TypeNeedsSqlClient,
            TypeNeedsNewerSqlClient,
            QueryShapeNeedsRows,
            DriverNotReferenced,
            MethodNameTaken,
            TokenNamedAsAnArgument,
            ExtensionClassNameTaken,
            NotAConverter,
            DuplicateConverter,
            ConverterTypeMismatch,
            ConverterDirectionMissing
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
            SqlParseErrorKind.UnknownGeneratorParameter => UnknownGeneratorParameter,
            SqlParseErrorKind.EmptyGeneratorLine => EmptyGeneratorLine,
            SqlParseErrorKind.InvalidMarkerValue => InvalidMarkerValue,
            SqlParseErrorKind.ConflictingSettings => ConflictingSettings,
            SqlParseErrorKind.EmptyBlock => EmptyBlock,
            SqlParseErrorKind.ReservedTokenName => ReservedTokenName,
            SqlParseErrorKind.MisplacedDialect => MisplacedDialect,
            SqlParseErrorKind.MarkerNotAllowedHere => MarkerNotAllowedHere,
            SqlParseErrorKind.MissingParameterType => MissingParameterType,
            SqlParseErrorKind.UndeclaredParameter => UndeclaredParameter,
            SqlParseErrorKind.InputModelWithoutParameters => InputModelWithoutParameters,
            SqlParseErrorKind.TokenCrossesComment => TokenCrossesComment,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No descriptor is defined for this kind."),
        };
}
