using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.SqlServer;

// SQL Server refuses a parameter used twice, so each use of a reused parameter is renamed to a copy of its own; the
// copies' types are unified into the parameter's, and the server's list of parameters is held to the lexer's.
public sealed class ParameterCopiesTests
{
    private static readonly SqlServerType Int = new("int", 4, 10, 0, null);

    private static readonly SqlServerType Name = new("nvarchar(100)", 200, 0, 0, null);

    private static readonly SqlServerType Xml = new("xml", -1, 0, 0, null);

    private static readonly IReadOnlyDictionary<string, string> NoCopies = new Dictionary<string, string>();

    [Fact]
    public void Rename_ParameterUsedOnce_IsLeftAsWritten()
    {
        var copied = Rename("SELECT id FROM users WHERE id = @p");

        copied.Sql.ShouldBe("SELECT id FROM users WHERE id = @p");
        copied.Reused.ShouldBeEmpty();
        copied.ParameterOfCopy.ShouldBeEmpty();
    }

    [Fact]
    public void Rename_ParameterUsedTwice_IsTwoCopiesNumberedFromOne()
    {
        var copied = Rename("SELECT id FROM users WHERE (@name IS NULL OR name = @name)");

        copied.Sql.ShouldBe("SELECT id FROM users WHERE (@name_1 IS NULL OR name = @name_2)");
        copied.Reused.ShouldHaveSingleItem().Name.ShouldBe("name");
        copied.Reused[0].Copies.ShouldBe(["name_1", "name_2"]);
    }

    [Fact]
    public void Rename_ParameterUsedThreeTimes_IsThreeCopiesInOrder() =>
        Rename("SELECT @p, @p, @p").Sql.ShouldBe("SELECT @p_1, @p_2, @p_3");

    [Fact]
    public void Rename_TwoReusedParameters_AreEachNumberedFromOne()
    {
        var copied = Rename("SELECT @a, @b, @a, @b, @c");

        copied.Sql.ShouldBe("SELECT @a_1, @b_1, @a_2, @b_2, @c");
        copied.Reused.Count.ShouldBe(2);
        copied
            .Reused[0]
            .ShouldSatisfyAllConditions(
                reused => reused.Name.ShouldBe("a"),
                reused => reused.Copies.ShouldBe(["a_1", "a_2"])
            );
        copied
            .Reused[1]
            .ShouldSatisfyAllConditions(
                reused => reused.Name.ShouldBe("b"),
                reused => reused.Copies.ShouldBe(["b_1", "b_2"])
            );
    }

    // The list spells a parameter as the SQL first writes it, and every copy follows that spelling.
    [Fact]
    public void Rename_SpellingsThatDifferInCase_AreOneParameter()
    {
        var copied = Rename("SELECT id FROM users WHERE id = @p OR parent = @P");

        copied.Sql.ShouldBe("SELECT id FROM users WHERE id = @p_1 OR parent = @p_2");
        copied.ParameterOfCopy["P_2"].ShouldBe("p");
    }

    [Fact]
    public void Rename_CopyName_MapsBackToTheParameterIgnoringCase()
    {
        var copied = Rename("SELECT @p, @p");

        copied.ParameterOfCopy["p_1"].ShouldBe("p");
        copied.ParameterOfCopy["P_2"].ShouldBe("p");
        copied.ParameterOfCopy.ContainsKey("p").ShouldBeFalse();
    }

    // A declared parameter goes to the server declared, and a declared one can be used any number of times.
    [Fact]
    public void Rename_DeclaredParameterUsedTwice_IsLeftAsWritten()
    {
        var copied = ParameterCopies.Rename(
            "SELECT @p, @p",
            DescribeRequests.Of(SqlDialectRules.SqlServer, "SELECT @p, @p", ("p", "int")).Parameters
        );

        copied.Sql.ShouldBe("SELECT @p, @p");
        copied.Reused.ShouldBeEmpty();
    }

    // A marker can name a parameter without a type, -- param: @p null, and such a one is sent undeclared.
    [Fact]
    public void Rename_MarkedParameterWithoutAType_IsRenamed() =>
        ParameterCopies
            .Rename("SELECT @p, @p", new EquatableArray<SqlQueryParameter>([new("p", null, true, true)]))
            .Sql.ShouldBe("SELECT @p_1, @p_2");

    [Fact]
    public void Rename_TextThatHoldsACopyName_LengthensTheSeparator()
    {
        const string Sql = "DECLARE @p_1 int = 1; SELECT @p + @p_1, @p";

        var copied = ParameterCopies.Rename(Sql, Listed(Sql, "p"));

        copied.Sql.ShouldBe("DECLARE @p_1 int = 1; SELECT @p__1 + @p_1, @p__2");
        copied.Reused[0].Copies.ShouldBe(["p__1", "p__2"]);
        copied.ParameterOfCopy["p__2"].ShouldBe("p");
    }

    [Fact]
    public void Rename_TextThatHoldsACopyNameInAnotherCase_LengthensTheSeparator()
    {
        const string Sql = "DECLARE @P_2 int = 1; SELECT @p + @P_2, @p";

        ParameterCopies.Rename(Sql, Listed(Sql, "p")).Sql.ShouldBe("DECLARE @P_2 int = 1; SELECT @p__1 + @P_2, @p__2");
    }

    // A declared parameter that the SQL does not hold is sent in the declarations, so a copy is not named as it is.
    [Fact]
    public void Rename_ListThatHoldsACopyName_LengthensTheSeparator()
    {
        var parameters = new EquatableArray<SqlQueryParameter>([
            DescribeRequests.Inferred("p"),
            DescribeRequests.Declared("p_1", "int"),
        ]);

        ParameterCopies.Rename("SELECT @p, @p", parameters).Sql.ShouldBe("SELECT @p__1, @p__2");
    }

    // SQL Server's identifier holds 128 characters with the "@", so a copy of a long name has its name cut to fit.
    [Fact]
    public void Rename_NameOfTheLongestLength_IsCutToFit()
    {
        var name = new string('a', 127);
        var sql = $"SELECT @{name}, @{name}";

        var copied = Rename(sql);

        var stem = new string('a', 125);
        copied.Reused[0].Copies.ShouldBe([stem + "_1", stem + "_2"]);
        copied.Sql.ShouldBe($"SELECT @{stem}_1, @{stem}_2");
        copied.ParameterOfCopy[stem + "_2"].ShouldBe(name);
    }

    // Two long names that differ only past the cut get copies of their own: the later one passes over the numbers the
    // first took.
    [Fact]
    public void Rename_TwoLongNamesAlikeUpToTheCut_HaveCopiesThatDiffer()
    {
        var first = new string('a', 126) + "x";
        var second = new string('A', 126) + "y";
        var sql = $"SELECT @{first}, @{second}, @{first}, @{second}";

        var copied = Rename(sql);

        var stem = new string('a', 125);
        var upper = new string('A', 125);
        copied.Reused[0].Copies.ShouldBe([stem + "_1", stem + "_2"]);
        copied.Reused[1].Copies.ShouldBe([upper + "_3", upper + "_4"]);
        copied.Sql.ShouldBe($"SELECT @{stem}_1, @{upper}_3, @{stem}_2, @{upper}_4");
        copied.ParameterOfCopy[upper + "_3"].ShouldBe(second);
    }

    [Fact]
    public void Rename_CutCopyNameTheTextHolds_IsPassedOver()
    {
        var name = new string('a', 127);
        var stem = new string('a', 125);
        var sql = $"DECLARE @{stem}_1 int = 1; SELECT @{name} + @{stem}_1, @{name}";

        var copied = ParameterCopies.Rename(sql, Listed(sql, name));

        copied.Reused[0].Copies.ShouldBe([stem + "_2", stem + "_3"]);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(124)]
    [InlineData(125)]
    [InlineData(126)]
    [InlineData(127)]
    public void Rename_LongName_EveryCopyFitsAnIdentifier(int length)
    {
        var name = new string('p', length);
        var sql = string.Join(", ", Enumerable.Repeat("@" + name, 12));

        var copies = Rename("SELECT " + sql).Reused[0].Copies;

        copies.Count.ShouldBe(12);
        copies.ShouldAllBe(copy => copy.Length <= ParameterCopies.MaximumCopyLength);
        copies.ShouldBeUnique();
    }

    // A name that is itself too long for an identifier is not cut, so the server refuses its copies with the error it
    // gives the name used once, which quotes the same start of the name.
    [Theory]
    [InlineData(128)]
    [InlineData(200)]
    public void Rename_NameLongerThanAnIdentifier_IsNotCut(int length)
    {
        var name = new string('p', length);
        var sql = $"SELECT @{name}, @{name}";

        var copied = Rename(sql);

        copied.Reused[0].Copies.ShouldBe([name + "_1", name + "_2"]);
        copied.Sql.ShouldBe($"SELECT @{name}_1, @{name}_2");
    }

    // A DECLAREd variable is in no list, so it is never reused, however often it is used.
    [Fact]
    public void Rename_DeclaredVariableUsedTwice_IsLeftAsWritten()
    {
        const string Sql = "DECLARE @v int = 1; SELECT @v + @v, @p";

        var copied = ParameterCopies.Rename(Sql, Listed(Sql, "p"));

        copied.Sql.ShouldBe(Sql);
        copied.Reused.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("SELECT '@p', @p")]
    [InlineData("SELECT @p -- @p")]
    [InlineData("SELECT @p /* @p */")]
    [InlineData("SELECT [@p], @p")]
    [InlineData("SELECT \"@p\", @p")]
    [InlineData("SELECT @@ROWCOUNT, @p")]
    public void Rename_NameOutsideAParameter_IsLeftAsWritten(string sql) =>
        ParameterCopies.Rename(sql, Listed(sql, "p")).Sql.ShouldBe(sql);

    [Fact]
    public void Rename_ReusedParameterBesideAString_RenamesOnlyTheParameters()
    {
        const string Sql = "SELECT '@p', @p, [@p], @p";

        ParameterCopies.Rename(Sql, Listed(Sql, "p")).Sql.ShouldBe("SELECT '@p', @p_1, [@p], @p_2");
    }

    [Fact]
    public void Agreed_EveryTypeEqual_IsTheType() => ParameterCopies.Agreed([Name, Name with { }]).ShouldBe(Name);

    [Fact]
    public void Agreed_AnUntypedCopy_IsNull() => ParameterCopies.Agreed([Name, null]).ShouldBeNull();

    [Fact]
    public void Agreed_TwoTypes_IsNull() => ParameterCopies.Agreed([Int, Name]).ShouldBeNull();

    [Fact]
    public void Agreed_NoCopy_IsNull() => ParameterCopies.Agreed([]).ShouldBeNull();

    // A type is its name, its facets and its user type: dbo.Email is not the nvarchar(320) it stands over.
    [Fact]
    public void Agreed_SameTypeWithAndWithoutAUserType_IsNull()
    {
        var plain = new SqlServerType("nvarchar(320)", 640, 0, 0, null);
        var email = plain with { UserType = new SqlServerUserType("dbo", "Email", null) };

        ParameterCopies.Agreed([plain, email]).ShouldBeNull();
    }

    [Fact]
    public void Unify_EveryCopyAgrees_IsTheType() =>
        ParameterCopies.Unify([new(Name, false), new(Name, false)]).Value.ShouldBe(Name);

    // (@name_1 IS NULL OR name = @name_2): the first copy binds as xml and as int, and constrains nothing.
    [Fact]
    public void Unify_CopyIgnoredBesideATypedOne_IsTheTypedOnesType() =>
        ParameterCopies.Unify([new(Int, true), new(Name, false)]).Value.ShouldBe(Name);

    // A copy in a context of xml binds as xml and not as int, so it is not ignored.
    [Fact]
    public void Unify_CopyInAContextOfXml_IsKept() =>
        ParameterCopies.Unify([new(Int, true), new(Xml, false)]).Value.ShouldBe(Xml);

    [Fact]
    public void Unify_EveryCopyIgnored_FailsWithNoType() =>
        ParameterCopies.Unify([new(Int, true), new(Int, true)]).Failure.ShouldBe(new CopiesFailure(null, null));

    [Fact]
    public void Unify_TwoTypesLeft_FailsWithTheFirstTwoInOrder() =>
        ParameterCopies
            .Unify([new(Int, true), new(Name, false), new(Name, false), new(Int, false), new(Xml, false)])
            .Failure.ShouldBe(new CopiesFailure(Name, Int));

    [Fact]
    public void Unify_UntypedCopyLeft_FailsWithNoType() =>
        ParameterCopies.Unify([new(Name, false), new(null, false)]).Failure.ShouldBe(new CopiesFailure(null, null));

    [Fact]
    public void CompareLists_EqualLists_HaveNoDifference() =>
        ParameterCopies.CompareLists(["id", "name"], ["@name", "@id"], NoCopies).ShouldBeEmpty();

    // CREATE PROCEDURE p @id int: the lexer reads @id as a parameter, and the server as the procedure's own.
    [Fact]
    public void CompareLists_ListedParameterTheServerLacks_IsReadBySqlSource() =>
        ParameterCopies
            .CompareLists(["id", "name"], ["@name"], NoCopies)
            .ShouldBe([new ListDifference("id", ReadBySqlSource: true)]);

    // A name the lexer took for a DECLAREd variable, which the server reports as a parameter.
    [Fact]
    public void CompareLists_ReportedParameterTheListLacks_IsNotReadBySqlSource() =>
        ParameterCopies
            .CompareLists(["id"], ["@id", "@x"], NoCopies)
            .ShouldBe([new ListDifference("x", ReadBySqlSource: false)]);

    [Fact]
    public void CompareLists_DifferencesBothWays_AreTheListsThenTheServers() =>
        ParameterCopies
            .CompareLists(["b", "id", "a"], ["@y", "@id", "@x"], NoCopies)
            .ShouldBe([new("b", true), new("a", true), new("y", false), new("x", false)]);

    [Fact]
    public void CompareLists_ReportedCopies_AreTakenBackToTheirParameter() =>
        ParameterCopies
            .CompareLists(
                ["p", "id"],
                ["@p_1", "@P_2", "@id"],
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["p_1"] = "p", ["p_2"] = "p" }
            )
            .ShouldBeEmpty();

    [Fact]
    public void CompareLists_NamesInAnotherCase_AreTheSame() =>
        ParameterCopies.CompareLists(["UserId"], ["@userid"], NoCopies).ShouldBeEmpty();

    // A copy of a parameter the server reported while the list lacks it is named once, by its parameter.
    [Fact]
    public void CompareLists_TwoReportedCopiesTheListLacks_AreOneDifference() =>
        ParameterCopies
            .CompareLists(
                [],
                ["@p_1", "@p_2"],
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["p_1"] = "p", ["p_2"] = "p" }
            )
            .ShouldBe([new ListDifference("p", false)]);

    // A request as the plan makes one, which has no DECLAREd variable in the SQL.
    private static CopiedSql Rename(string sql) =>
        ParameterCopies.Rename(sql, DescribeRequests.Of(SqlDialectRules.SqlServer, sql).Parameters);

    // The plan leaves a DECLAREd variable out of the list, which DescribeRequests.Of does not.
    private static EquatableArray<SqlQueryParameter> Listed(string sql, params string[] names)
    {
        var parameters = new SqlQueryParameter[names.Length];
        for (var index = 0; index < names.Length; index++)
        {
            parameters[index] = DescribeRequests.Inferred(names[index]);
        }

        return DescribeRequests.Listed(sql, parameters).Parameters;
    }
}
