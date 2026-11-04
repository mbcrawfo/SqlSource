using System;
using Shouldly;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.Tool.Tests.Realistic;

// The reading of the claims of a corpus's RealisticMethods.cs from its source: a class whose attribute's Source names
// the query.sql of a folder of Cases claims that case, and any other path, or any argument but Source, fails naming the
// class.  The corpus tests that each engine's cases are claimed as they should be are in each engine's project.
public sealed class RealisticClaimsTests
{
    [Fact]
    public void Parse_ClassWithPathAttribute_IsItsClaim() =>
        RealisticClaims
            .Parse(
                """
                [SqlSourceGenerate(Source = "../Cases/a-b/query.sql")]
                internal static partial class ABMethod;
                """
            )
            .ShouldBe([new RealisticClaim("ABMethod", "a-b")]);

    [Theory]
    [InlineData("SqlSource.SqlSourceGenerate")]
    [InlineData("global::SqlSourceGenerate")]
    public void Parse_QualifiedAttributeName_IsItsClaim(string attribute) =>
        RealisticClaims
            .Parse(
                $$"""
                [{{attribute}}(Source = "../Cases/a-b/query.sql")]
                internal static partial class ABMethod;
                """
            )
            .ShouldBe([new RealisticClaim("ABMethod", "a-b")]);

    [Theory]
    [InlineData("Output = GeneratorOutput.CodeGen", "Output")]
    [InlineData("Missing = 1", "Missing")]
    public void Parse_ClaimWithAnArgumentOtherThanSource_ThrowsNamingTheClassAndTheArgument(
        string argument,
        string name
    )
    {
        var thrown = Should.Throw<InvalidOperationException>(() =>
            RealisticClaims.Parse(
                $$"""
                [SqlSourceGenerate(Source = "../Cases/a-b/query.sql", {{argument}})]
                internal static partial class XMethod;
                """
            )
        );

        thrown.Message.ShouldContain("XMethod");
        thrown.Message.ShouldContain(name);
    }

    [Fact]
    public void Parse_ClassWithAnotherAttribute_IsNoClaim() =>
        RealisticClaims
            .Parse(
                """
                [Obsolete("../Cases/a-b/query.sql")]
                internal static partial class ABQuery;

                internal static partial class Plain;
                """
            )
            .ShouldBeEmpty();

    [Fact]
    public void Parse_AttributeWithoutSource_ThrowsNamingTheClass()
    {
        var thrown = Should.Throw<InvalidOperationException>(() =>
            RealisticClaims.Parse(
                """
                [SqlSourceGenerate]
                internal static partial class XMethod;
                """
            )
        );

        thrown.Message.ShouldContain("XMethod");
        thrown.Message.ShouldContain("no path");
    }

    [Fact]
    public void Parse_PathOutsideCases_Throws()
    {
        var thrown = Should.Throw<InvalidOperationException>(() =>
            RealisticClaims.Parse(
                """
                [SqlSourceGenerate(Source = "Queries/x.sql")]
                internal static partial class XQuery;
                """
            )
        );

        thrown.Message.ShouldContain("Queries/x.sql");
    }
}
