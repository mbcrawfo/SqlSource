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

public class SqlDiagnosticsTests
{
    [Fact]
    public void All_EveryDescriptor_IsAnErrorThatCannotBeConfigured()
    {
        foreach (var descriptor in SqlDiagnostics.All)
        {
            descriptor.DefaultSeverity.ShouldBe(DiagnosticSeverity.Error, descriptor.Id);
            descriptor.IsEnabledByDefault.ShouldBeTrue(descriptor.Id);
            descriptor.Category.ShouldBe("SqlSource", descriptor.Id);
            descriptor.CustomTags.ShouldContain(WellKnownDiagnosticTags.NotConfigurable, descriptor.Id);
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
                "https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc" + descriptor.Id[6..]
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
}
