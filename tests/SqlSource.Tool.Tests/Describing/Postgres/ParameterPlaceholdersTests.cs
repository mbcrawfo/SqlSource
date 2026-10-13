using System;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// What is sent for a query: its one statement with $n where the lexer finds a parameter, and the type list of the
// PREPARE.  A text in which the lexer finds several statements is never sent.
public class ParameterPlaceholdersTests
{
    private static EquatableArray<SqlQueryParameter> List(params SqlQueryParameter[] parameters) =>
        new([.. parameters]);

    private static SqlQueryParameter Inferred(string name) => new(name, null, null, false);

    private static SqlQueryParameter Declared(string name, string type) => new(name, type, null, true);

    [Fact]
    public void Rewrite_ParameterOnceAndTwice_HasOneOrdinal() =>
        ParameterPlaceholders
            .Rewrite("SELECT @id, @id + 1 FROM users WHERE id = @id", List(Inferred("id")))
            .ShouldBe("SELECT $1, $1 + 1 FROM users WHERE id = $1");

    [Fact]
    public void Rewrite_TwoParameters_AreNumberedByTheList() =>
        ParameterPlaceholders
            .Rewrite("SELECT 1 WHERE @b = 1 AND @a = 2", List(Inferred("b"), Inferred("a"), Declared("c", "int")))
            .ShouldBe("SELECT 1 WHERE $1 = 1 AND $2 = 2");

    [Fact]
    public void Rewrite_NameInAnotherCase_IsTheSameParameter() =>
        ParameterPlaceholders.Rewrite("SELECT @ID, @Id", List(Inferred("id"))).ShouldBe("SELECT $1, $1");

    [Theory]
    [InlineData("SELECT '@id'")]
    [InlineData("SELECT 1 -- @id\nFROM users")]
    [InlineData("SELECT /* @id */ 1")]
    [InlineData("SELECT \"@id\" FROM users")]
    [InlineData("SELECT $q$ @id $q$")]
    public void Rewrite_AtSignThatIsNoParameter_IsLeftAlone(string sql) =>
        ParameterPlaceholders.Rewrite(sql, List()).ShouldBe(sql);

    [Fact]
    public void Rewrite_TrailingSemicolonWithSpaceAndCommentsAfterIt_IsDroppedWithThem() =>
        ParameterPlaceholders
            .Rewrite("DELETE FROM users WHERE id = @id ; -- done\n/* end */ ", List(Inferred("id")))
            .ShouldBe("DELETE FROM users WHERE id = $1 ");

    [Fact]
    public void Rewrite_SemicolonInAStringOrADollarQuote_IsKept() =>
        ParameterPlaceholders.Rewrite("SELECT ';', $x$;$x$", List()).ShouldBe("SELECT ';', $x$;$x$");

    [Theory]
    [InlineData("SELECT 1; SELECT 2")]
    [InlineData("SELECT 1; SELECT 2;")]
    [InlineData("SELECT 1;;")]
    [InlineData("SELECT 1; -- one\nDROP TABLE users")]
    public void Rewrite_SecondStatement_IsNull(string sql) => ParameterPlaceholders.Rewrite(sql, List()).ShouldBeNull();

    // The lexer reads "$x$" after a "§" or a "£" as the start of a dollar-quoted string, since neither is a letter
    // to it, and so the ";" is in a string and the text is one statement.  The server reads "§$x$" as one
    // identifier, and the text as two statements and a comment.  That the two disagree is safe: the PREPARE goes to
    // the driver as a batch of one command, which the driver does not split, and the server refuses a text of two
    // statements whole.  tests/SqlSource.Tests.Postgres holds that nothing of it runs.
    [Theory]
    [InlineData("SELECT 1 AS §$x$; DROP TABLE users; --$x$")]
    [InlineData("SELECT 1 AS £$x$; DROP TABLE users; --$x$")]
    public void Rewrite_DollarQuoteThatTheServerReadsAsAnIdentifier_IsReturnedWhole(string sql) =>
        ParameterPlaceholders.Rewrite(sql, List()).ShouldBe(sql);

    // The plan's parser made the list from the same SQL, so a parameter outside it is a bug of the plan.
    [Fact]
    public void Rewrite_ParameterTheListDoesNotHold_Throws() =>
        Should
            .Throw<InvalidOperationException>(() => ParameterPlaceholders.Rewrite("SELECT @id", List()))
            .Message.ShouldContain("'id'");

    [Fact]
    public void TypeList_DeclaredAndInferred_HasTheTypeAsWrittenOrUnknownInTheListsOrder()
    {
        var types = ParameterPlaceholders.TypeList(
            List(Inferred("id"), Declared("name", " varchar(50) "), Declared("tenant", "public.tenant_id"))
        );

        types.Value.ShouldBe(["unknown", "varchar(50)", "public.tenant_id"]);
    }

    [Fact]
    public void TypeList_TypeThatIsNoTypeName_GivesItsParameter()
    {
        var refused = Declared("b", "int) AS SELECT 1");

        var types = ParameterPlaceholders.TypeList(List(Declared("a", "int"), refused, Declared("c", "nope nope")));

        types.Value.ShouldBeNull();
        types.Failure.ShouldBe(refused);
    }

    // The list ends on a line of its own: whatever a type's text held, the ") AS" is not on its line.
    [Fact]
    public void PrepareText_WithTypes_EndsTheListWithALineBreakBeforeAs() =>
        ParameterPlaceholders
            .PrepareText("sqlsource_1", ["unknown", "int"], "SELECT $1, $2")
            .ShouldBe("PREPARE sqlsource_1 (unknown, int\n) AS SELECT $1, $2");

    // "PREPARE s () AS" is a syntax error.
    [Fact]
    public void PrepareText_WithoutTypes_HasNoList() =>
        ParameterPlaceholders.PrepareText("sqlsource_1", [], "SELECT 1").ShouldBe("PREPARE sqlsource_1 AS SELECT 1");

    [Fact]
    public void HeadLength_OfAPrepare_IsWhatStandsBeforeTheSql()
    {
        ParameterPlaceholders.HeadLength("sqlsource_6", ["unknown"]).ShouldBe(34);
        ParameterPlaceholders.HeadLength("sqlsource_4", []).ShouldBe(23);
    }

    // The server counts characters, and one outside the basic plane is two chars of a string.
    [Fact]
    public void HeadLength_TypeWithACharacterOutsideTheBasicPlane_CountsItOnce() =>
        ParameterPlaceholders
            .HeadLength("sqlsource_1", ["\"\U0001F600\""])
            .ShouldBe(ParameterPlaceholders.HeadLength("sqlsource_1", ["\"x\""]));

    // "PREPARE sqlsource_10 (unknown, nope_t" and then a line break and ")": unknown is the characters 23 to 29 and
    // nope_t 32 to 37.  A position between two items, or after the last, belongs to the item before it.
    [Theory]
    [InlineData(1, 0)]
    [InlineData(23, 0)]
    [InlineData(29, 0)]
    [InlineData(31, 0)]
    [InlineData(32, 1)]
    [InlineData(37, 1)]
    [InlineData(39, 1)]
    public void TypeAt_Position_IsTheItemItFallsInOrFollows(int position, int expected) =>
        ParameterPlaceholders.TypeAt("sqlsource_10", ["unknown", "nope_t"], position).ShouldBe(expected);
}
