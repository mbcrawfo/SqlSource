using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Tool.Bundles.Renaming.SqlServer;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles.Renaming.SqlServer;

public sealed class SqlServerVocabularyTests
{
    public static TheoryData<string> Lists =>
        [
            "Reserved",
            "Odbc",
            "Future",
            "NonReserved",
            "PseudoColumns",
            "GlobalVariables",
            "Functions",
            "Types",
            "Schemas",
            "SystemDatabases",
        ];

    [Fact]
    public void Lists_OfTheKeywords_HoldTheCountsOfThePage()
    {
        SqlServerVocabulary.Reserved.Count.ShouldBe(185);
        SqlServerVocabulary.Odbc.Count.ShouldBe(235);
        SqlServerVocabulary.Future.Count.ShouldBe(273);
        SqlServerVocabulary.Future.ShouldContain("REGR_R2");
    }

    [Theory]
    [InlineData("select")]
    [InlineData("SeLeCt")]
    [InlineData("$ACTION")]
    [InlineData("@@rowcount")]
    [InlineData("Msdb")]
    [InlineData(SqlServerVocabulary.HiddenRow)]
    [InlineData("geography")]
    public void Contains_AWordOfTheServer_IsTrue(string spelling) =>
        SqlServerVocabulary.Contains(spelling).ShouldBeTrue(spelling);

    [Theory]
    [InlineData("users")]
    [InlineData("ROWSTAT")]
    public void Contains_AnotherWord_IsFalse(string spelling) =>
        SqlServerVocabulary.Contains(spelling).ShouldBeFalse(spelling);

    [Theory]
    [MemberData(nameof(Lists))]
    public void Lists_EachOne_HoldsNoWordTwice(string name)
    {
        var list = (IReadOnlyList<string>)typeof(SqlServerVocabulary).GetProperty(name)!.GetValue(null)!;
        var folded = list.Select(static word => word.ToUpperInvariant()).ToList();
        folded.Count.ShouldBe(folded.Distinct().Count(), name);
    }
}
