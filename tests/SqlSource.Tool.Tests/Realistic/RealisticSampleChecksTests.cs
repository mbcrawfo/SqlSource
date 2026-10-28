using System;
using System.Collections.Generic;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.Tool.Tests.Realistic;

// The checks of a case's sample values against the query's parameters, and how a sample's value is found by name.
public sealed class RealisticSampleChecksTests
{
    [Fact]
    public void CheckKeys_KeyThatNamesNoParameter_ThrowsNamingTheCaseAndTheKey()
    {
        var found = Case(RealisticSample.Of(("from", "'2024-01-01'"), ("tto", "'2025-01-01'")));

        var thrown = Should.Throw<InvalidOperationException>(() =>
            RealisticSampleChecks.CheckKeys(found, ["from", "to"])
        );

        thrown.Message.ShouldContain("realistic/keyed");
        thrown.Message.ShouldContain("@tto");
    }

    [Fact]
    public void CheckKeys_KeyThatDiffersInCaseAlone_DoesNotThrow() =>
        Should.NotThrow(() =>
            RealisticSampleChecks.CheckKeys(Case(RealisticSample.Of(("From", "'2024-01-01'"))), ["from", "to"])
        );

    // A run never sends NULL for a parameter that its marker does not say is null, so a sample does not either.
    [Theory]
    [InlineData("NULL")]
    [InlineData(" null ")]
    public void CheckNulls_NullForAParameterNotMarkedNull_ThrowsNamingTheCaseAndTheParameter(string value)
    {
        var found = Case(RealisticSample.Of(("Status", value)));

        var thrown = Should.Throw<InvalidOperationException>(() =>
            RealisticSampleChecks.CheckNulls(found, [Parameter("status", null), Parameter("q", true)])
        );

        thrown.Message.ShouldContain("realistic/keyed");
        thrown.Message.ShouldContain("@Status");
    }

    [Fact]
    public void CheckNulls_NullForAParameterNotNullByItsMarker_Throws() =>
        Should.Throw<InvalidOperationException>(() =>
            RealisticSampleChecks.CheckNulls(Case(RealisticSample.Of(("status", "NULL"))), [Parameter("status", false)])
        );

    [Theory]
    [InlineData("NULL", true)]
    [InlineData("'NULL'", false)]
    public void CheckNulls_NullForAParameterMarkedNullOrNoNull_DoesNotThrow(string value, bool? nullable) =>
        Should.NotThrow(() =>
            RealisticSampleChecks.CheckNulls(
                Case(RealisticSample.Of(("status", value))),
                [Parameter("status", nullable)]
            )
        );

    // A dictionary that compares by ordinal still finds a name written in another case.
    [Theory]
    [InlineData("status", "'open'")]
    [InlineData("STATUS", "'open'")]
    [InlineData("state", null)]
    public void Find_Name_IsTheValueByTheNameIgnoringCase(string name, string? value) =>
        RealisticSampleChecks
            .Find(new Dictionary<string, string>(StringComparer.Ordinal) { ["Status"] = "'open'" }, name)
            .ShouldBe(value);

    private static SqlQueryParameter Parameter(string name, bool? nullable) => new(name, null, nullable, true);

    private static RealisticCase Case(IReadOnlyDictionary<string, string> sample) =>
        RealisticTestCases.Keyed() with
        {
            Samples = [sample],
        };
}
