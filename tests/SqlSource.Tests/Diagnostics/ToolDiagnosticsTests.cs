using System;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Diagnostics;
using Xunit;

namespace SqlSource.Tests.Diagnostics;

// The errors of the sqlsource tool are descriptors of the generator assembly, held to the rules of the generator's.
public class ToolDiagnosticsTests
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
    public void All_Ids_AreUniqueInOrderAndFrom200()
    {
        var ids = ToolDiagnostics.All.Select(descriptor => descriptor.Id).ToArray();

        ids.ShouldBe(ids.Distinct().OrderBy(id => id, StringComparer.Ordinal));
        ids.ShouldAllBe(id => id.StartsWith("SQLSRC", StringComparison.Ordinal) && id.Length == 9);
        foreach (var id in ids)
        {
            int.Parse(id.AsSpan(6), CultureInfo.InvariantCulture).ShouldBeGreaterThanOrEqualTo(200, id);
        }
    }

    [Fact]
    public void All_Ids_ComeAfterEveryIdOfTheGenerator() =>
        string.CompareOrdinal(SqlDiagnostics.All[^1].Id, ToolDiagnostics.All[0].Id).ShouldBeLessThan(0);

    [Fact]
    public void All_HelpLinks_PointAtTheDescriptorsSectionOfTheDiagnosticsDocument()
    {
        foreach (var descriptor in ToolDiagnostics.All)
        {
            // The anchor of a heading is its text in lower case, and an id is "SQLSRC" followed by digits.
            descriptor.HelpLinkUri.ShouldBe(
                "https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc" + descriptor.Id[6..]
            );
        }
    }

    // RS1032, which the build checks, allows a period only at the end of a message of several sentences.  The tool
    // formats a message itself, so nothing else would notice a placeholder that the arguments do not fill.
    [Fact]
    public void All_Messages_FormatWithTheirArguments()
    {
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.UnexpectedFailure.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "System.Exception",
                "boom"
            )
            .ShouldBe("sqlsource failed unexpectedly: System.Exception: boom");

        foreach (
            var descriptor in ToolDiagnostics
                .All.Remove(ToolDiagnostics.UnexpectedFailure)
                .Remove(ToolDiagnostics.UnnamedConnectionNotUsed)
        )
        {
            // The later arguments are for the ones that have more than one place: a format ignores an argument it
            // has no place for.
            string.Format(
                    CultureInfo.InvariantCulture,
                    descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture),
                    "/work/app",
                    "the reason",
                    "a third",
                    "a fourth"
                )
                .ShouldContain("'/work/app'", Case.Sensitive, descriptor.Id);
        }
    }

    [Fact]
    public void ProjectCannotBeEvaluated_Message_HoldsTheProject() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.ProjectCannotBeEvaluated.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "/work/App.csproj"
            )
            .ShouldBe("MSBuild could not evaluate '/work/App.csproj'");

    [Fact]
    public void ManifestCannotBeRead_Message_HoldsTheProjectAndTheReason() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.ManifestCannotBeRead.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "/work/App.csproj",
                "it names no project"
            )
            .ShouldBe("The project manifest of '/work/App.csproj' cannot be read: it names no project");

    [Fact]
    public void ProjectNotInRun_Message_HoldsThePathAndTheUnit() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.ProjectNotInRun.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "/work/Other/Other.csproj",
                "/work/App.slnx"
            )
            .ShouldBe("'/work/Other/Other.csproj' is not a project of '/work/App.slnx'");

    [Fact]
    public void SolutionCannotBeRead_Message_HoldsTheSolutionAndTheReason() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.SolutionCannotBeRead.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "/work/App.sln",
                "Not a solution file."
            )
            .ShouldBe("'/work/App.sln' cannot be read: Not a solution file.");

    [Fact]
    public void DirectoryCannotBeRead_Message_HoldsTheDirectoryAndTheReason() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.DirectoryCannotBeRead.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "/srv/locked",
                "Access is denied."
            )
            .ShouldBe("'/srv/locked' cannot be read: Access is denied.");

    [Fact]
    public void AttributeArgumentNotLiteral_Message_HoldsTheArgument() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.AttributeArgumentNotLiteral.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "Path"
            )
            .ShouldBe(
                "'Path' of [SqlSourceGenerate] is read from the source by 'sqlsource', which needs a literal here"
            );

    [Fact]
    public void OutputNeedsDescribableDialect_Message_HoldsTheOutputAndTheDialect() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.OutputNeedsDescribableDialect.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "codegen",
                "ansi"
            )
            .ShouldBe(
                "The output 'codegen' needs a dialect that can be described, and the dialect of this file is 'ansi'.  "
                    + "Set the dialect to 'postgres' or 'mssql', or the output to 'sql'."
            );

    [Fact]
    public void TokenHasNoDefault_Message_HoldsTheTokenAndTheOutput() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.TokenHasNoDefault.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "where",
                "models"
            )
            .ShouldBe(
                "The token 'where' has no default.  A query whose output is 'models' is described with a sample in "
                    + "its place."
            );

    [Fact]
    public void DatabaseHasTwoDialects_Message_HoldsTheDatabaseTheTwoDialectsAndTheOtherFile() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.DatabaseHasTwoDialects.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "main",
                "mssql",
                "postgres",
                "/work/App/Users.sql"
            )
            .ShouldBe("The database 'main' has the dialect 'mssql' here and 'postgres' in '/work/App/Users.sql'");

    [Fact]
    public void FileNotInRun_Message_HoldsThePath() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.FileNotInRun.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "/work/App/User.sql"
            )
            .ShouldBe("'/work/App/User.sql' is not a .sql file that a type of the run claims");

    [Fact]
    public void DatabaseHasNoConnection_Message_HoldsTheDatabase() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.DatabaseHasNoConnection.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "billing"
            )
            .ShouldBe("No connection is given for the database 'billing'");

    [Fact]
    public void DatabasesShareConnectionVariable_Message_HoldsBothDatabasesAndTheVariable() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.DatabasesShareConnectionVariable.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "app-v2",
                "app_v2",
                "SQLSOURCE_CONNECTION_APP_V2"
            )
            .ShouldBe(
                "The databases 'app-v2' and 'app_v2' both read their connection from SQLSOURCE_CONNECTION_APP_V2"
            );

    [Fact]
    public void UnnamedConnectionNotUsed_Message_HoldsTheCountAndTheNames() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.UnnamedConnectionNotUsed.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "2",
                "billing, reports"
            )
            .ShouldBe("SQLSOURCE_CONNECTION is for a run with one database, and this run has 2: billing, reports");

    [Fact]
    public void NoDescriberForDialect_Message_HoldsTheDialect() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.NoDescriberForDialect.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "postgres"
            )
            .ShouldBe("This version of sqlsource cannot describe 'postgres'");

    [Fact]
    public void SidecarOfNewerTool_Message_HoldsTheSidecarAndBothVersions() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.SidecarOfNewerTool.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "/work/Users.sql.json",
                "2",
                "1"
            )
            .ShouldBe("'/work/Users.sql.json' has format 2, and this tool writes format 1");

    [Fact]
    public void SidecarNotWritten_Message_HoldsTheSqlFile() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.SidecarNotWritten.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "/work/Users.sql"
            )
            .ShouldBe(
                "The sidecar of '/work/Users.sql' was not written, because not every query of the file has an entry"
            );

    [Theory]
    [InlineData("written")]
    [InlineData("deleted")]
    public void FileCannotBeChanged_Message_HoldsTheFileWhatWasTriedAndTheReason(string tried) =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.FileCannotBeChanged.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "/work/Users.sql.json",
                tried,
                "Access is denied."
            )
            .ShouldBe($"'/work/Users.sql.json' could not be {tried}: Access is denied.");
}
