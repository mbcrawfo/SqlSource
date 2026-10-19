using System;
using System.IO;
using System.Linq;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.Postgres.Realistic.Generation;

// The claim of each case by a class of RealisticQueries.cs: a case with models has exactly one, a case refused by the
// describer or the generator has none, and a claim names a folder of Cases.  So a case added without its class, or
// a case that comes to be refused or stops being so, fails naming itself.  It reads the source and needs no server.
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

    [Theory]
    [MemberData(nameof(RealisticCasesTests.Cases), MemberType = typeof(RealisticCasesTests))]
    public void All_Claims_AreTheCasesWithModelsEachOnce(string name)
    {
        var found = RealisticCases.Read(Path.Combine(RepositoryPaths.RealisticCases, name));

        var claims = RealisticClaims.All.Count(claim => claim.Case == name);

        claims.ShouldBe(found.HasModels ? 1 : 0, RealisticClaims.ClaimedBy(found, claims));
    }

    [Fact]
    public void All_Claims_NameOnlyCasesThatExist() =>
        RealisticClaims.All.Select(static claim => claim.Case).ShouldBeSubsetOf(RealisticCases.Names);
}
