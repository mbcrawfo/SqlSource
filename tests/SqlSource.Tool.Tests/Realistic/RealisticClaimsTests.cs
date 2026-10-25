using System;
using Shouldly;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.Tool.Tests.Realistic;

// The reading of the claims of a corpus's RealisticQueries.cs and RealisticMethods.cs from their source: a class whose
// attribute's Path names the query.sql of a folder of Cases claims that case at the member of GeneratorOutput its
// Output names, and any other path or Output fails naming the class.  The corpus tests that each engine's cases are
// claimed as they should be are in each engine's project.
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
            .ShouldBe([new RealisticClaim("ABQuery", "a-b", "Models")]);

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
            .ShouldBe([new RealisticClaim("ABQuery", "a-b", null)]);

    [Fact]
    public void Parse_ClaimAtCodeGen_HasThatOutput() =>
        RealisticClaims
            .Parse(
                """
                [SqlSourceGenerate(Path = "../Cases/a-b/query.sql", Output = GeneratorOutput.CodeGen)]
                internal static partial class ABMethod;
                """
            )
            .ShouldBe([new RealisticClaim("ABMethod", "a-b", "CodeGen")]);

    [Fact]
    public void Parse_QualifiedOutput_IsItsMemberName() =>
        RealisticClaims
            .Parse(
                """
                [SqlSourceGenerate(Path = "../Cases/a-b/query.sql", Output = SqlSource.GeneratorOutput.CodeGen)]
                internal static partial class ABMethod;
                """
            )
            .ShouldBe([new RealisticClaim("ABMethod", "a-b", "CodeGen")]);

    [Fact]
    public void Parse_OutputThatIsNotAMemberAccess_ThrowsNamingTheClass()
    {
        var thrown = Should.Throw<InvalidOperationException>(() =>
            RealisticClaims.Parse(
                """
                [SqlSourceGenerate(Path = "../Cases/a-b/query.sql", Output = "CodeGen")]
                internal static partial class XMethod;
                """
            )
        );

        thrown.Message.ShouldContain("XMethod");
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
