using System;
using System.Collections.Generic;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Postgres.Realistic;

// The sample values of a case: their substitution into its SQL and their checks against the query's parameters.
public sealed class RealisticSamplesTests
{
    [Fact]
    public void Substitute_ParameterInAStringACommentOrAQuotedName_IsLeftAlone() =>
        RealisticSamples
            .Substitute("SELECT '@x', \"@x\", @x -- @x", Values(("x", "1")), Values(("x", "integer")))
            .ShouldBe("SELECT '@x', \"@x\", (1)::integer -- @x");

    [Fact]
    public void Substitute_TwoParameters_ReplacesEachWithItsCastLiteral() =>
        RealisticSamples
            .Substitute(
                "WHERE a = @a AND b = @B",
                Values(("a", "'x'"), ("b", "NULL")),
                Values(("a", "text"), ("b", "bigint"))
            )
            .ShouldBe("WHERE a = ('x')::text AND b = (NULL)::bigint");

    [Fact]
    public void Substitute_ParameterNotInTheSample_ThrowsNamingIt() =>
        Should
            .Throw<InvalidOperationException>(() =>
                RealisticSamples.Substitute(
                    "WHERE a = @a AND c = @c",
                    Values(("a", "1")),
                    Values(("a", "integer"), ("c", "integer"))
                )
            )
            .Message.ShouldContain("@c");

    [Fact]
    public void CheckKeys_KeyThatNamesNoParameter_ThrowsNamingTheCaseAndTheKey()
    {
        var found = Case(Values(("from", "'2024-01-01'"), ("tto", "'2025-01-01'")));

        var thrown = Should.Throw<InvalidOperationException>(() => RealisticSamples.CheckKeys(found, ["from", "to"]));

        thrown.Message.ShouldContain("realistic/keyed");
        thrown.Message.ShouldContain("@tto");
    }

    [Fact]
    public void CheckKeys_KeyThatDiffersInCaseAlone_DoesNotThrow() =>
        Should.NotThrow(() => RealisticSamples.CheckKeys(Case(Values(("From", "'2024-01-01'"))), ["from", "to"]));

    // A run never sends NULL for a parameter that its marker does not say is null, so a sample does not either.
    [Theory]
    [InlineData("NULL")]
    [InlineData(" null ")]
    public void CheckNulls_NullForAParameterNotMarkedNull_ThrowsNamingTheCaseAndTheParameter(string value)
    {
        var found = Case(Values(("Status", value)));

        var thrown = Should.Throw<InvalidOperationException>(() =>
            RealisticSamples.CheckNulls(found, [Parameter("status", null), Parameter("q", true)])
        );

        thrown.Message.ShouldContain("realistic/keyed");
        thrown.Message.ShouldContain("@Status");
    }

    [Fact]
    public void CheckNulls_NullForAParameterNotNullByItsMarker_Throws() =>
        Should.Throw<InvalidOperationException>(() =>
            RealisticSamples.CheckNulls(Case(Values(("status", "NULL"))), [Parameter("status", false)])
        );

    [Theory]
    [InlineData("NULL", true)]
    [InlineData("'NULL'", false)]
    public void CheckNulls_NullForAParameterMarkedNullOrNoNull_DoesNotThrow(string value, bool? nullable) =>
        Should.NotThrow(() =>
            RealisticSamples.CheckNulls(Case(Values(("status", value))), [Parameter("status", nullable)])
        );

    private static Dictionary<string, string> Values(params (string Name, string Value)[] values)
    {
        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in values)
        {
            found.Add(name, value);
        }

        return found;
    }

    private static SqlQueryParameter Parameter(string name, bool? nullable) => new(name, null, nullable, true);

    private static RealisticCase Case(Dictionary<string, string> sample) =>
        new(
            "keyed",
            "keyed",
            null,
            null,
            [],
            [new Dictionary<string, string>(sample, StringComparer.OrdinalIgnoreCase)]
        );
}
