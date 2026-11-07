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
            "ClauseWords",
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
    [InlineData("SP_EXECUTESQL")]
    [InlineData("content")]
    [InlineData("Document")]
    public void Contains_AWordOfTheServer_IsTrue(string spelling) =>
        SqlServerVocabulary.Contains(spelling).ShouldBeTrue(spelling);

    // The words of the table hints, the query hints, a window's frame, FOR JSON, FOR XML and FOR SYSTEM_TIME, and the
    // table-valued functions that the table of contents of the documentation lacks.
    [Theory]
    [InlineData("FOLLOWING")]
    [InlineData("NOLOCK")]
    [InlineData("READPAST")]
    [InlineData("UPDLOCK")]
    [InlineData("ROWLOCK")]
    [InlineData("TABLOCK")]
    [InlineData("TABLOCKX")]
    [InlineData("PAGLOCK")]
    [InlineData("XLOCK")]
    [InlineData("NOWAIT")]
    [InlineData("READUNCOMMITTED")]
    [InlineData("READCOMMITTED")]
    [InlineData("REPEATABLEREAD")]
    [InlineData("SERIALIZABLE")]
    [InlineData("SNAPSHOT")]
    [InlineData("FORCESEEK")]
    [InlineData("FORCESCAN")]
    [InlineData("NOEXPAND")]
    [InlineData("MAXDOP")]
    [InlineData("HASH")]
    [InlineData("LOOP")]
    [InlineData("OPTIMIZE")]
    [InlineData("UNKNOWN")]
    [InlineData("FAST")]
    [InlineData("KEEPFIXED")]
    [InlineData("EXPAND")]
    [InlineData("VIEWS")]
    [InlineData("AUTO")]
    [InlineData("RAW")]
    [InlineData("ROOT")]
    [InlineData("INCLUDE_NULL_VALUES")]
    [InlineData("WITHOUT_ARRAY_WRAPPER")]
    [InlineData("ELEMENTS")]
    [InlineData("TYPE")]
    [InlineData("CONTAINED")]
    [InlineData("STRING_SPLIT")]
    [InlineData("GENERATE_SERIES")]
    [InlineData("CHANGETABLE")]
    [InlineData("PREDICT")]
    public void Contains_AWordOfAClauseOrATableValuedFunction_IsTrue(string spelling) =>
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
