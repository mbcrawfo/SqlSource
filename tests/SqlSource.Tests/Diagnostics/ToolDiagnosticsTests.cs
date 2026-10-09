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

        foreach (var descriptor in ToolDiagnostics.All.Remove(ToolDiagnostics.UnexpectedFailure))
        {
            string.Format(
                    CultureInfo.InvariantCulture,
                    descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture),
                    "/work/app"
                )
                .ShouldContain("'/work/app'", Case.Sensitive, descriptor.Id);
        }
    }
}
