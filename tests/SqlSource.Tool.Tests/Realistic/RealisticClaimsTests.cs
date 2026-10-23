using System;
using Shouldly;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.Tool.Tests.Realistic;

// The reading of the claims of a corpus's RealisticQueries.cs from its source: a class whose attribute's Path names
// the query.sql of a folder of Cases claims that case, and any other path fails naming the class.  The corpus tests
// that each engine's cases are claimed as they should be are in each engine's project.
public sealed class RealisticClaimsTests
{
    [Fact]
    public void Parse_ClassWithPathAttribute_IsItsClaim() =>
        RealisticClaims
            .Parse(
                """
                [SqlSourceGenerate(Path = "../Cases/a-b/query.sql", Output = GeneratorOutput.Models)]
                internal static partial class ABQuery;
                """
            )
            .ShouldBe([new RealisticClaim("ABQuery", "a-b")]);

    [Theory]
    [InlineData("SqlSource.SqlSourceGenerate")]
    [InlineData("global::SqlSourceGenerate")]
    public void Parse_QualifiedAttributeName_IsItsClaim(string attribute) =>
        RealisticClaims
            .Parse(
                $$"""
                [{{attribute}}(Path = "../Cases/a-b/query.sql")]
                internal static partial class ABQuery;
                """
            )
            .ShouldBe([new RealisticClaim("ABQuery", "a-b")]);

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
    public void Parse_AttributeWithoutPath_ThrowsNamingTheClass()
    {
        var thrown = Should.Throw<InvalidOperationException>(() =>
            RealisticClaims.Parse(
                """
                [SqlSourceGenerate(Output = GeneratorOutput.Models)]
                internal static partial class XQuery;
                """
            )
        );

        thrown.Message.ShouldContain("XQuery");
        thrown.Message.ShouldContain("no path");
    }

    [Fact]
    public void Parse_PathOutsideCases_Throws()
    {
        var thrown = Should.Throw<InvalidOperationException>(() =>
            RealisticClaims.Parse(
                """
                [SqlSourceGenerate(Path = "Queries/x.sql")]
                internal static partial class XQuery;
                """
            )
        );

        thrown.Message.ShouldContain("Queries/x.sql");
    }
}
