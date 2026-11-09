using Shouldly;
using SqlSource.Generation.Methods;
using SqlSource.Parsing;
using SqlSource.Settings;
using SqlSource.Testing.Snapshot;
using Xunit;

namespace SqlSource.Tests.Generation.Methods;

public sealed class ListNamesTests
{
    [Fact]
    public void PrefixOf_Plain_IsTheNameAndAnUnderscore()
    {
        var parameters = Of(List("ids"), Scalar("status"));

        ListNames.PrefixOf(parameters[0], 0, parameters, None, DatabaseEngine.PostgreSql).ShouldBe("ids_");
    }

    [Fact]
    public void PrefixOf_AnotherParameterMatchesTheSuffix_AddsAnUnderscore()
    {
        var parameters = Of(List("ids"), Scalar("ids_0"));

        ListNames.PrefixOf(parameters[0], 0, parameters, None, DatabaseEngine.PostgreSql).ShouldBe("ids__");
    }

    [Fact]
    public void PrefixOf_ANameOfAnotherCase_ComparesIgnoringCase()
    {
        var parameters = Of(List("ids"), Scalar("IDS_3"));

        ListNames.PrefixOf(parameters[0], 0, parameters, None, DatabaseEngine.SqlServer).ShouldBe("ids__");
    }

    // ids_ followed by a name character that is no digit is no name the list sends.
    [Fact]
    public void PrefixOf_AnotherParameterHasTheSuffixAndALetter_KeepsThePrefix()
    {
        var parameters = Of(List("ids"), Scalar("ids_a"), Scalar("ids_"));

        ListNames.PrefixOf(parameters[0], 0, parameters, None, DatabaseEngine.PostgreSql).ShouldBe("ids_");
    }

    [Fact]
    public void PrefixOf_EachMatchedInTurn_AddsAnUnderscoreForEach()
    {
        var parameters = Of(Scalar("ids__12"), List("ids"), Scalar("ids_0"));

        ListNames.PrefixOf(parameters[1], 0, parameters, None, DatabaseEngine.PostgreSql).ShouldBe("ids___");
    }

    // a's prefix is pushed to a__, the prefix a_ would take, so the later list takes another underscore.
    [Fact]
    public void PrefixOf_AnEarlierListTookThePrefix_AddsAnUnderscore()
    {
        var parameters = Of(List("a"), List("a_"), Scalar("a_5"));

        ListNames.PrefixOf(parameters[0], 0, parameters, None, DatabaseEngine.PostgreSql).ShouldBe("a__");
        ListNames.PrefixOf(parameters[1], 1, parameters, None, DatabaseEngine.PostgreSql).ShouldBe("a___");
    }

    // SQL Server refuses a parameter named as a variable the query declares, which is none of its parameters.
    [Fact]
    public void PrefixOf_ADeclaredVariableMatchesTheSuffix_AddsAnUnderscore()
    {
        var parameters = Of(List("ids"));

        ListNames.PrefixOf(parameters[0], 0, parameters, new(["ids_0"]), DatabaseEngine.SqlServer).ShouldBe("ids__");
    }

    // A SQL Server name holds at most 128 characters, the @ among them, and an index takes up to four digits.
    [Fact]
    public void PrefixOf_TooLongForSqlServer_UsesTheOrdinal()
    {
        var longest = new string('a', 122);
        var tooLong = new string('b', 123);
        var parameters = Of(List(longest), List(tooLong), Scalar("list1_0"));

        ListNames.PrefixOf(parameters[0], 0, parameters, None, DatabaseEngine.SqlServer).ShouldBe(longest + "_");
        ListNames.PrefixOf(parameters[1], 1, parameters, None, DatabaseEngine.SqlServer).ShouldBe("list1__");
        ListNames.PrefixOf(parameters[1], 1, parameters, None, DatabaseEngine.PostgreSql).ShouldBe(tooLong + "_");
    }

    private static readonly EquatableArray<string> None = EquatableArray<string>.Empty;

    private static SqlQueryParameter List(string name) => new(name, null, null, false, IsList: true);

    private static SqlQueryParameter Scalar(string name) => new(name, null, null, false);

    private static EquatableArray<SqlQueryParameter> Of(params SqlQueryParameter[] parameters) =>
        TestSidecars.Of(parameters);
}
