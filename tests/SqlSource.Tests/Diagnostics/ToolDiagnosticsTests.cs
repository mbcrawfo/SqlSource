using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Diagnostics;
using Xunit;
using static SqlSource.Testing.DiagnosticMessages;

namespace SqlSource.Tests.Diagnostics;

// The errors of the sqlsource tool are descriptors of the generator assembly, held to the rules of the generator's.
public sealed class ToolDiagnosticsTests
{
    [Fact]
    public void All_EveryDescriptor_IsAnErrorThatCannotBeConfigured()
    {
        foreach (var descriptor in ToolDiagnostics.All)
        {
            descriptor.DefaultSeverity.ShouldBe(DiagnosticSeverity.Error, descriptor.Id);
            descriptor.IsEnabledByDefault.ShouldBeTrue(descriptor.Id);
            descriptor.Category.ShouldBe("SqlSource", descriptor.Id);
            descriptor.CustomTags.ShouldContain(WellKnownDiagnosticTags.NotConfigurable, descriptor.Id);
        }
    }

    [Fact]
    public void All_Ids_AreFrom200()
    {
        foreach (var descriptor in ToolDiagnostics.All)
        {
            int.Parse(descriptor.Id.AsSpan(6), CultureInfo.InvariantCulture)
                .ShouldBeGreaterThanOrEqualTo(200, descriptor.Id);
        }
    }

    [Fact]
    public void All_Ids_AreUniqueAcrossBothClasses()
    {
        var ids = SqlDiagnostics.All.Concat(ToolDiagnostics.All).Select(descriptor => descriptor.Id).ToArray();

        ids.ShouldBeUnique();
    }

    [Theory]
    [InlineData("SqlDiagnostics")]
    [InlineData("ToolDiagnostics")]
    public void All_Ids_AreUniqueInOrderAndOfTheFormSqlsrcAndThreeDigits(string descriptors)
    {
        var ids = AllOf(descriptors).Select(descriptor => descriptor.Id).ToArray();

        ids.ShouldBe(ids.Distinct().OrderBy(id => id, StringComparer.Ordinal));
        ids.ShouldAllBe(id => id.StartsWith("SQLSRC", StringComparison.Ordinal) && id.Length == 9);
    }

    [Theory]
    [InlineData("SqlDiagnostics")]
    [InlineData("ToolDiagnostics")]
    public void All_HelpLinks_PointAtTheDescriptorsSectionOfTheDiagnosticsDocument(string descriptors)
    {
        foreach (var descriptor in AllOf(descriptors))
        {
            // The anchor of a heading is its text in lower case, and an id is "SQLSRC" followed by digits.
            descriptor.HelpLinkUri.ShouldBe(
                "https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/diagnostics.md#sqlsrc" + descriptor.Id[6..]
            );
        }
    }

    // The tool formats a message itself, so nothing else would notice a placeholder that the arguments do not fill.
    [Fact]
    public void All_Messages_FormatWithTheirArguments()
    {
        Format(ToolDiagnostics.UnexpectedFailure, "System.Exception", "boom")
            .ShouldBe("sqlsource failed unexpectedly: System.Exception: boom");

        // These start with a path that is already named, in quotes or by its position, as the tool passes it.
        var namesItsPath = new[]
        {
            ToolDiagnostics.NoRunUnit,
            ToolDiagnostics.SeveralRunUnits,
            ToolDiagnostics.NotARunUnit,
            ToolDiagnostics.ProjectNotInRun,
            ToolDiagnostics.FileNotInRun,
            ToolDiagnostics.DirectoryCannotBeRead,
            ToolDiagnostics.ManifestCannotBeRead,
            ToolDiagnostics.FileCannotBeChanged,
        };
        // SQLSRC200 is formatted above, and SQLSRC215 starts with a count, SQLSRC231 with the name of the reader,
        // SQLSRC266 with a column's position, SQLSRC288 to SQLSRC291 with a sentence about the bundle, which the
        // path they are reported at names, SQLSRC292 with a query's name, SQLSRC293 and SQLSRC294 with a sentence
        // about a renamed bundle, and SQLSRC296 with one about a raw bundle; their own tests format them.
        foreach (
            var descriptor in ToolDiagnostics
                .All.Remove(ToolDiagnostics.UnexpectedFailure)
                .Remove(ToolDiagnostics.UnnamedConnectionNotUsed)
                .Remove(ToolDiagnostics.ParametersReadDifferently)
                .Remove(ToolDiagnostics.ColumnHasNoName)
                .Remove(ToolDiagnostics.BundleCannotBeRead)
                .Remove(ToolDiagnostics.BundleOfNewerTool)
                .Remove(ToolDiagnostics.BundleCallMissing)
                .Remove(ToolDiagnostics.NoDescriberForBundle)
                .Remove(ToolDiagnostics.QueryNotInFile)
                .Remove(ToolDiagnostics.BundleCannotBeMadeSafe)
                .Remove(ToolDiagnostics.RenamedBundleDoesNotReproduce)
                .Remove(ToolDiagnostics.RawBundleNotReproduced)
        )
        {
            // The later arguments are for the ones that have more than one place: a format ignores an argument it
            // has no place for.
            Format(
                    descriptor,
                    namesItsPath.Contains(descriptor) ? "'/work/app'" : "/work/app",
                    "the reason",
                    "a third",
                    "a fourth"
                )
                .ShouldContain("'/work/app'", Case.Sensitive, descriptor.Id);
        }
    }

    [Fact]
    public void Format_BundleMessages_AreFormattedWithTheirArguments()
    {
        Format(ToolDiagnostics.BundleCannotBeRead, "the reason").ShouldBe("The bundle cannot be read: the reason");
        Format(ToolDiagnostics.BundleOfNewerTool, "format", 3, 2)
            .ShouldBe("The bundle's format is 3, and this version of sqlsource reads 2");
        Format(ToolDiagnostics.BundleCallMissing, "a question", "sqlsource 1.4.0", "1.6.0")
            .ShouldBe(
                "The bundle has no answer to 'a question' that the query asks: it was made by sqlsource 1.4.0, "
                    + "and this is sqlsource 1.6.0"
            );
        Format(ToolDiagnostics.NoDescriberForBundle, "mysql")
            .ShouldBe("The bundle is of the engine 'mysql', which this version of sqlsource does not describe");
    }

    [Fact]
    public void Format_DiagnoseMessages_AreFormattedWithTheirArguments()
    {
        Format(ToolDiagnostics.QueryNotInFile, "GetUser", "/work/App/Users.sql", "the reason")
            .ShouldBe("There is no query 'GetUser' to diagnose in '/work/App/Users.sql': the reason");
        Format(ToolDiagnostics.BundleCannotBeMadeSafe, "the reason", "query.sql", "customer_ssn")
            .ShouldBe("The bundle cannot be made safe to post: the reason, at query.sql, found customer_ssn");
        Format(ToolDiagnostics.RenamedBundleDoesNotReproduce, "expected.json")
            .ShouldBe("The renamed bundle does not reproduce the query's answer: its replay differs at expected.json");
        Format(ToolDiagnostics.BundleIsNotRaw, "/work/report.zip", "the reason")
            .ShouldBe("'/work/report.zip' is not a raw bundle: the reason");
        Format(ToolDiagnostics.RawBundleNotReproduced, "columns[0].nullable: true -> false", "sqlsource 1.4.0", "1.6.0")
            .ShouldBe(
                "This version of sqlsource does not reproduce the bundle's answer, so it is not renamed: "
                    + "columns[0].nullable: true -> false; it was made by sqlsource 1.4.0, and this is sqlsource 1.6.0"
            );
    }

    [Fact]
    public void DotEnvCannotBeRead_Message_HoldsThePathAndTheReason() =>
        Format(ToolDiagnostics.DotEnvCannotBeRead, "/work/.env", "Access to the path is denied.")
            .ShouldBe("'/work/.env' cannot be read: Access to the path is denied.");

    [Fact]
    public void DotEnvLineCannotBeRead_Message_HoldsTheKeyAndTheReason() =>
        Format(ToolDiagnostics.DotEnvLineCannotBeRead, "SQLSOURCE_CONNECTION_A", "a quote is not closed")
            .ShouldBe("The line that sets 'SQLSOURCE_CONNECTION_A' cannot be read: a quote is not closed");

    [Fact]
    public void WatchFailed_Message_HoldsTheFolderAndTheReason() =>
        Format(ToolDiagnostics.WatchFailed, "/work/App", "The inotify limit was reached.")
            .ShouldBe("watching '/work/App' for changes failed: The inotify limit was reached.");

    [Fact]
    public void ProjectCannotBeEvaluated_Message_HoldsTheProject() =>
        Format(ToolDiagnostics.ProjectCannotBeEvaluated, "/work/App.csproj")
            .ShouldBe("MSBuild could not evaluate '/work/App.csproj'");

    // The manifest MSBuild wrote for a project is named by the project, and the file of --manifest by itself, which
    // is printed in full: one that holds "=" or ";" is refused unless the build wrote it, under --online.
    [Theory]
    [InlineData(
        "of '/work/App.csproj'",
        "The project manifest of '/work/App.csproj' cannot be read: it names no project"
    )]
    [InlineData(
        "'/work/obj/App.manifest'",
        "The project manifest '/work/obj/App.manifest' cannot be read: it names no project"
    )]
    public void ManifestCannotBeRead_Message_HoldsWhatItNamesAndTheReason(string named, string expected) =>
        Format(ToolDiagnostics.ManifestCannotBeRead, named, "it names no project").ShouldBe(expected);

    [Fact]
    public void ProjectNotInRun_Message_HoldsThePathAndTheUnit() =>
        Format(ToolDiagnostics.ProjectNotInRun, "'/work/Other/Other.csproj'", "/work/App.slnx")
            .ShouldBe("'/work/Other/Other.csproj' is not a project of '/work/App.slnx'");

    [Fact]
    public void SolutionCannotBeRead_Message_HoldsTheSolutionAndTheReason() =>
        Format(ToolDiagnostics.SolutionCannotBeRead, "/work/App.sln", "Not a solution file.")
            .ShouldBe("'/work/App.sln' cannot be read: Not a solution file.");

    [Fact]
    public void DirectoryCannotBeRead_Message_HoldsTheDirectoryAndTheReason() =>
        Format(ToolDiagnostics.DirectoryCannotBeRead, "'/srv/locked'", "Access is denied.")
            .ShouldBe("'/srv/locked' cannot be read: Access is denied.");

    [Fact]
    public void AttributeArgumentNotLiteral_Message_HoldsTheArgument() =>
        Format(ToolDiagnostics.AttributeArgumentNotLiteral, "Source")
            .ShouldBe(
                "'Source' of [SqlSourceGenerate] is read from the source by 'sqlsource', which needs a literal here"
            );

    [Fact]
    public void OutputNeedsDescribableDialect_Message_HoldsTheOutputAndTheDialect() =>
        Format(ToolDiagnostics.OutputNeedsDescribableDialect, "methods", "ansi")
            .ShouldBe(
                "The output 'methods' needs each query described by its database, and the dialect 'ansi' has no "
                    + "database to ask.  Set the dialect to 'postgres' or 'mssql', or the output to 'sql'."
            );

    [Fact]
    public void TokenHasNoDefault_Message_HoldsTheTokenAndTheOutput() =>
        Format(ToolDiagnostics.TokenHasNoDefault, "where", "models")
            .ShouldBe(
                "The token 'where' has no default, which a query whose output is 'models' needs.  Give it a default, "
                    + "or set the output to 'sql'."
            );

    [Fact]
    public void DatabaseHasTwoDialects_Message_HoldsTheDatabaseTheTwoDialectsAndTheOtherFile() =>
        Format(ToolDiagnostics.DatabaseHasTwoDialects, "main", "mssql", "postgres", "/work/App/Users.sql")
            .ShouldBe("The database 'main' has the dialect 'mssql' here and 'postgres' in '/work/App/Users.sql'");

    [Fact]
    public void FileNotInRun_Message_HoldsThePath() =>
        Format(ToolDiagnostics.FileNotInRun, "'/work/App/User.sql'")
            .ShouldBe("'/work/App/User.sql' is not a .sql file that a type of the run claims");

    [Fact]
    public void DatabaseHasNoConnection_Message_HoldsTheDatabase() =>
        Format(ToolDiagnostics.DatabaseHasNoConnection, "billing")
            .ShouldBe("No connection is given for the database 'billing'");

    [Fact]
    public void DatabasesShareConnectionVariable_Message_HoldsBothDatabasesAndTheVariable() =>
        Format(ToolDiagnostics.DatabasesShareConnectionVariable, "app-v2", "app_v2", "SQLSOURCE_CONNECTION_APP_V2")
            .ShouldBe(
                "The databases 'app-v2' and 'app_v2' both read their connection from SQLSOURCE_CONNECTION_APP_V2"
            );

    [Fact]
    public void UnnamedConnectionNotUsed_Message_HoldsTheCountAndTheNames() =>
        Format(ToolDiagnostics.UnnamedConnectionNotUsed, "2", "billing, reports")
            .ShouldBe("SQLSOURCE_CONNECTION is for a run with one database, and this run has 2: billing, reports");

    [Fact]
    public void NoDescriberForDialect_Message_HoldsTheDialect() =>
        Format(ToolDiagnostics.NoDescriberForDialect, "postgres")
            .ShouldBe("This version of sqlsource cannot describe 'postgres'");

    [Fact]
    public void SidecarOfNewerTool_Message_HoldsTheSidecarAndBothVersions() =>
        Format(ToolDiagnostics.SidecarOfNewerTool, "/work/Users.sql.json", "2", "1")
            .ShouldBe("'/work/Users.sql.json' has format 2, and this tool writes format 1");

    [Fact]
    public void SidecarNotWritten_Message_HoldsTheSqlFile() =>
        Format(ToolDiagnostics.SidecarNotWritten, "/work/Users.sql")
            .ShouldBe(
                "The sidecar of '/work/Users.sql' was not written, because not every query of the file has an entry"
            );

    // The file is named in quotes.
    [Theory]
    [InlineData("'/work/Users.sql.json'", "written")]
    [InlineData("'/work/Users.sql.json'", "deleted")]
    public void FileCannotBeChanged_Message_HoldsTheFileWhatWasTriedAndTheReason(string named, string tried) =>
        Format(ToolDiagnostics.FileCannotBeChanged, named, tried, "Access is denied.")
            .ShouldBe($"{named} could not be {tried}: Access is denied.");

    [Fact]
    public void DatabaseCannotBeOpened_Message_HoldsTheDatabase() =>
        Format(ToolDiagnostics.DatabaseCannotBeOpened, "app").ShouldBe("The database 'app' could not be opened");

    [Fact]
    public void DeclaredTypeNotValid_Message_HoldsTheTypeTheParameterTheQueryAndTheReason() =>
        Format(ToolDiagnostics.DeclaredTypeNotValid, "nope_t", "@p", "GetUser", "the database has no such type")
            .ShouldBe(
                "The type 'nope_t' declared for the parameter '@p' of the query 'GetUser' is not valid: the database "
                    + "has no such type"
            );

    [Fact]
    public void ColumnTypeNotSupported_Message_HoldsTheColumnTheQueryAndTheType() =>
        Format(ToolDiagnostics.ColumnTypeNotSupported, "row", "ListPairs", "record")
            .ShouldBe("The column 'row' of the query 'ListPairs' has the type 'record', which cannot be described");

    [Fact]
    public void QueryHasSeveralStatements_Message_HoldsTheQuery() =>
        Format(ToolDiagnostics.QueryHasSeveralStatements, "Cleanup")
            .ShouldBe("The query 'Cleanup' holds more than one statement");

    [Fact]
    public void ParametersReadDifferently_Message_HoldsTheReaderTheQueryAndWhatDiffers() =>
        Format(
                ToolDiagnostics.ParametersReadDifferently,
                "Npgsql",
                "ListUsers",
                "column 1 is text for Npgsql and integer for SqlSource"
            )
            .ShouldBe(
                "Npgsql reads the parameters of the query 'ListUsers' differently from SqlSource: "
                    + "column 1 is text for Npgsql and integer for SqlSource"
            );

    [Fact]
    public void CopiesHaveDifferentTypes_Message_HoldsTheParameterTheQueryAndBothTypes() =>
        Format(ToolDiagnostics.CopiesHaveDifferentTypes, "@p", "FindUsers", "int", "nvarchar(100)")
            .ShouldBe(
                "The copies of the parameter '@p' of the query 'FindUsers' have the types 'int' and 'nvarchar(100)'"
            );

    [Fact]
    public void TypeCannotBeBound_Message_HoldsTheTypeTheParameterAndTheQuery() =>
        Format(ToolDiagnostics.TypeCannotBeBound, "public.d2", "@p", "ListUsers")
            .ShouldBe("Npgsql cannot bind the type 'public.d2' of the parameter '@p' of the query 'ListUsers'");

    [Fact]
    public void ServerRejectedQuery_Message_HoldsTheQueryAndTheServersMessage() =>
        Format(ToolDiagnostics.ServerRejectedQuery, "GetUser", "relation \"nope\" does not exist")
            .ShouldBe("The server rejected the query 'GetUser': relation \"nope\" does not exist");

    [Fact]
    public void ParameterTypeNotDetermined_Message_HoldsTheParameterAndTheQuery() =>
        Format(ToolDiagnostics.ParameterTypeNotDetermined, "@p", "IsNull")
            .ShouldBe("The type of the parameter '@p' of the query 'IsNull' could not be determined");

    [Fact]
    public void ParameterFirstUseDoesNotFixType_Message_HoldsTheParameterAndTheQuery() =>
        Format(ToolDiagnostics.ParameterFirstUseDoesNotFixType, "@name", "FindUsers")
            .ShouldBe("The first use of the parameter '@name' of the query 'FindUsers' does not fix its type");

    [Fact]
    public void FileNotUtf8_Message_HoldsTheFile() =>
        Format(ToolDiagnostics.FileNotUtf8, "/work/App/Queries/Users.sql")
            .ShouldBe(
                "'/work/App/Queries/Users.sql' is not saved as UTF-8, "
                    + "so its text depends on the system that reads it.  Save it as UTF-8."
            );

    private static ImmutableArray<DiagnosticDescriptor> AllOf(string name) =>
        name switch
        {
            "SqlDiagnostics" => SqlDiagnostics.All,
            "ToolDiagnostics" => ToolDiagnostics.All,
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
        };
}
