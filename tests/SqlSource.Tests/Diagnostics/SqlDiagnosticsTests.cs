using System;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Diagnostics;

public sealed class SqlDiagnosticsTests
{
    // The warnings a project may configure, each with a comment on its descriptor that says why.  Every other
    // diagnostic is an error that a project cannot turn off.
    private static readonly string[] Configurable = ["SQLSRC240", "SQLSRC245"];

    [Fact]
    public void All_EveryDescriptor_IsAnErrorThatCannotBeConfiguredButTheListedWarnings()
    {
        foreach (var descriptor in SqlDiagnostics.All)
        {
            descriptor.IsEnabledByDefault.ShouldBeTrue(descriptor.Id);
            descriptor.Category.ShouldBe("SqlSource", descriptor.Id);
            if (Configurable.Contains(descriptor.Id))
            {
                descriptor.DefaultSeverity.ShouldBe(DiagnosticSeverity.Warning, descriptor.Id);
                descriptor.CustomTags.ShouldNotContain(WellKnownDiagnosticTags.NotConfigurable, descriptor.Id);
            }
            else
            {
                descriptor.DefaultSeverity.ShouldBe(DiagnosticSeverity.Error, descriptor.Id);
                descriptor.CustomTags.ShouldContain(WellKnownDiagnosticTags.NotConfigurable, descriptor.Id);
            }
        }

        foreach (var id in Configurable)
        {
            SqlDiagnostics.All.ShouldContain(descriptor => descriptor.Id == id, id);
        }
    }

    [Fact]
    public void All_Ids_AreUniqueAndInOrder()
    {
        var ids = SqlDiagnostics.All.Select(descriptor => descriptor.Id).ToArray();

        ids.ShouldBe(ids.Distinct().OrderBy(id => id, StringComparer.Ordinal));
        ids.ShouldAllBe(id => id.StartsWith("SQLSRC", StringComparison.Ordinal) && id.Length == 9);
    }

    [Fact]
    public void All_HelpLinks_PointAtTheDescriptorsSectionOfTheDiagnosticsDocument()
    {
        foreach (var descriptor in SqlDiagnostics.All)
        {
            // The anchor of a heading is its text in lower case, and an id is "SQLSRC" followed by digits.
            descriptor.HelpLinkUri.ShouldBe(
                "https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/diagnostics.md#sqlsrc" + descriptor.Id[6..]
            );
        }
    }

    [Fact]
    public void ForParseError_EveryKind_HasItsOwnDescriptorInTheFileRange()
    {
        var kinds = Enum.GetValues<SqlParseErrorKind>();

        var descriptors = kinds.Select(SqlDiagnostics.ForParseError).ToArray();

        descriptors.Distinct().Count().ShouldBe(kinds.Length);
        descriptors
            .Select(descriptor => descriptor.Id)
            .ShouldBe(kinds.Select((_, index) => string.Create(CultureInfo.InvariantCulture, $"SQLSRC{101 + index}")));
    }

    [Fact]
    public void ForParseError_InputModelWithoutParameters_IsSqlsrc119() =>
        SqlDiagnostics.ForParseError(SqlParseErrorKind.InputModelWithoutParameters).Id.ShouldBe("SQLSRC119");

    [Fact]
    public void InvalidDialect_Message_ListsTheNamesThatAreAccepted() =>
        SqlDiagnostics
            .InvalidDialect.MessageFormat.ToString(CultureInfo.InvariantCulture)
            .ShouldEndWith("SqlSourceDialect accepts " + SqlDialectName.Accepted + ".");

    [Fact]
    public void SidecarMalformed_Message_HoldsTheReason() =>
        Format(SqlDiagnostics.SidecarMalformed, "'hash' is missing")
            .ShouldBe(
                "The sidecar cannot be read, because 'hash' is missing.  Run 'sqlsource describe' to write it again."
            );

    [Fact]
    public void SidecarFormatTooNew_Message_HoldsBothVersions() =>
        Format(SqlDiagnostics.SidecarFormatTooNew, "2", "1")
            .ShouldBe(
                "The sidecar has format 2, and this version of SqlSource reads format 1.  Update the SqlSource package."
            );

    [Fact]
    public void SidecarFormatTooOld_Message_HoldsBothVersions() =>
        Format(SqlDiagnostics.SidecarFormatTooOld, "0", "1")
            .ShouldBe(
                "The sidecar has format 0, and this version of SqlSource reads format 1.  Run 'sqlsource describe' to "
                    + "write it again."
            );

    [Fact]
    public void SidecarToolVersionDiffers_Message_HoldsBothVersions() =>
        Format(SqlDiagnostics.SidecarToolVersionDiffers, "0.2.0-dev", "0.1.0")
            .ShouldBe(
                "The sidecar was written by sqlsource 0.2.0-dev, and this project uses SqlSource 0.1.0.  Bring the "
                    + "tool and the package to one version, and run 'sqlsource describe'."
            );

    [Fact]
    public void QueryHasNoEntry_Message_HoldsTheQueryAndTheOutput() =>
        Format(SqlDiagnostics.QueryHasNoEntry, "GetUser", "models")
            .ShouldBe(
                "The query 'GetUser' has no entry in the sidecar of its file, and its output 'models' needs one.  Run "
                    + "'sqlsource describe'."
            );

    [Fact]
    public void EntryEngineDiffers_Message_HoldsTheQueryTheEngineAndTheDialect() =>
        Format(SqlDiagnostics.EntryEngineDiffers, "GetUser", "mssql", "postgres")
            .ShouldBe(
                "The entry of the query 'GetUser' was described for 'mssql', and the dialect of this file is "
                    + "'postgres'.  Run 'sqlsource describe'."
            );

    [Fact]
    public void EngineHasNoTypeMap_Message_HoldsTheDialect() =>
        Format(SqlDiagnostics.EngineHasNoTypeMap, "mssql")
            .ShouldBe(
                "This version of SqlSource has no type map for the dialect 'mssql', so no query of this file can have "
                    + "models.  Set the output to 'sql'."
            );

    [Fact]
    public void EntryIsStale_Message_HoldsTheQuery() =>
        Format(SqlDiagnostics.EntryIsStale, "GetUser")
            .ShouldBe("The query 'GetUser' has changed since its entry was written.  Run 'sqlsource describe'.");

    [Fact]
    public void OrphanEntry_Message_HoldsTheEntry() =>
        Format(SqlDiagnostics.OrphanEntry, "GetUser")
            .ShouldBe(
                "The entry 'GetUser' names no query of the file that needs one.  Run 'sqlsource describe', which "
                    + "removes it."
            );

    [Fact]
    public void ForParseError_UndefinedKind_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => SqlDiagnostics.ForParseError((SqlParseErrorKind)(-1)));

    [Fact]
    public void ToDiagnostic_Info_GivesTheDescriptorLocationAndMessage()
    {
        var text = SourceText.From("SELECT 1;\n-- name: get-user\n");
        var info = DiagnosticInfo.Create(
            SqlDiagnostics.InvalidName,
            LocationInfo.From("/app/Users.sql", text, new TextSpan(19, 8)),
            "get-user"
        );

        var diagnostic = info.ToDiagnostic();

        diagnostic.Id.ShouldBe("SQLSRC103");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic
            .GetMessage(CultureInfo.InvariantCulture)
            .ShouldBe(
                "'get-user' is not a valid query name.  A name is a C# identifier that is not a reserved keyword."
            );
        var span = diagnostic.Location.GetLineSpan();
        span.Path.ShouldBe("/app/Users.sql");
        span.StartLinePosition.ShouldBe(new LinePosition(1, 9));
        span.EndLinePosition.ShouldBe(new LinePosition(1, 17));
        diagnostic.Location.SourceSpan.ShouldBe(new TextSpan(19, 8));
    }

    [Fact]
    public void Equals_TwoInfosWithTheSameContent_AreEqual()
    {
        var text = SourceText.From("SELECT 1;");

        static DiagnosticInfo Create(SourceText text) =>
            DiagnosticInfo.Create(
                SqlDiagnostics.DuplicateName,
                LocationInfo.From("/app/Users.sql", text, new TextSpan(0, 6)),
                "GetUser"
            );

        Create(text).ShouldBe(Create(text));
        Create(text).GetHashCode().ShouldBe(Create(text).GetHashCode());
    }

    private static string Format(DiagnosticDescriptor descriptor, params object[] arguments) =>
        string.Format(
            CultureInfo.InvariantCulture,
            descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture),
            arguments
        );
}
