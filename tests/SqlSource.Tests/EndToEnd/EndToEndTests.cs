using System;
using System.Linq;
using System.Reflection;
using Shouldly;
using SqlSource.Generation;
using Xunit;

namespace SqlSource.Tests.EndToEnd;

// The types of this folder are compiled by the build with the generator loaded, as a consumer's would be, and get
// their .sql files through the MSBuild file that the package ships.
public sealed class EndToEndTests
{
    [Fact]
    public void NestedLocation_DefaultFolder_HasAConstantForEachQueryOfEachFile()
    {
        UserQueries.GetUserSql.ShouldBe("SELECT id, name\nFROM users\nWHERE id = @id;");
        UserQueries.ListUsersSql.ShouldBe("SELECT id, name\nFROM users\nORDER BY name;");
        UserQueries.CountUsersSql.ShouldBe("SELECT COUNT(*) FROM users;");
    }

    [Fact]
    public void NestedLocation_SqlClass_IsPrivateStaticAndHoldsPublicConstants()
    {
        var sql = typeof(UserQueries).GetNestedType("Sql", BindingFlags.NonPublic).ShouldNotBeNull();

        sql.IsNestedPrivate.ShouldBeTrue();
        (sql.IsAbstract && sql.IsSealed).ShouldBeTrue();
        sql.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => field.Name)
            .ShouldBe(["CountUsers", "GetUser", "ListUsers"]);
    }

    [Fact]
    public void DirectLocation_PathToAFile_PutsTheConstantsOnTheType()
    {
        // Evaluated by the compiler: the member is a constant.
        const string Sql = OrderQueries.GetOrder;

        Sql.ShouldBe("SELECT id, total /* in cents */\nFROM orders\nWHERE id = @id;");
    }

    [Fact]
    public void DirectLocation_QueryWithTokens_IsAMethodThatReplacesEachOccurrence() =>
        OrderQueries
            .OrdersOf("sales", "u.name = @name")
            .ShouldBe(
                "-- The schema is a token, so this query is a method.\n"
                    + "SELECT o.id FROM sales.orders AS o INNER JOIN sales.users AS u ON u.id = o.user_id "
                    + "WHERE u.name = @name;"
            );

    [Fact]
    public void NestedLocation_QueryWithSeveralTokens_ReplacesEachOccurrence() =>
        TokenQueries
            .Search("id, name", "users", "name LIKE @pattern")
            .ShouldBe("SELECT id, name\nFROM users\nWHERE name LIKE @pattern\nORDER BY users.id;");

    [Fact]
    public void NestedLocation_QueryThatIsOnlyAToken_ReturnsTheArgument() =>
        TokenQueries.Whole("SELECT 1;").ShouldBe("SELECT 1;");

    // Eight tokens are more than one tuple holds: the eighth is read from the nested tuple's Rest.
    [Fact]
    public void NestedLocation_QueryWithEightTokens_ReplacesEachInItsPlace() =>
        TokenQueries.Eight("1", "2", "3", "4", "5", "6", "7", "8").ShouldBe("SELECT 12, 3, 4, 5, 6, 7, 8, 1;");

    // The sqlsource.json of this folder lists sort-input and no-token-validation as the generator parameters, and
    // Search has no list of its own.  That its method checks nothing shows the settings file reaching the generator.
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void ProjectWithValidationOff_QueryWithoutAGeneratorParameter_AcceptsAnArgumentWithoutText(string filter) =>
        TokenQueries
            .Search("id", "users", filter)
            .ShouldBe("SELECT id\nFROM users\nWHERE " + filter + "\nORDER BY users.id;");

    [Fact]
    public void ProjectWithValidationOff_QueryWithoutAGeneratorParameter_FailsOnANullArgumentWhereItIsRead() =>
        Should.Throw<NullReferenceException>(() => TokenQueries.Search("id", null!, "1 = 1"));

    [Theory]
    [InlineData("")]
    [InlineData(" \t")]
    public void ProjectWithValidationOff_QueryWithItsOwnList_RejectsAnArgumentWithoutText(string filter) =>
        Should.Throw<ArgumentException>(() => TokenQueries.Checked("users", filter)).ParamName.ShouldBe("filter");

    [Fact]
    public void ProjectWithValidationOff_QueryWithItsOwnList_RejectsANullArgument() =>
        Should.Throw<ArgumentNullException>(() => TokenQueries.Checked(null!, "1 = 1")).ParamName.ShouldBe("table");

    [Fact]
    public void ProjectWithValidationOff_QueryWithItsOwnList_ReturnsTheSqlForArgumentsWithText() =>
        TokenQueries.Checked("users", "id = @id").ShouldBe("SELECT id FROM users WHERE id = @id;");

    // The sqlsource.json of this folder sets the dialect to postgres.  ANSI would end the string at the quote after the
    // backslash and take the rest of the line for a comment.
    [Fact]
    public void ProjectWithADialect_FileWithoutItsOwn_IsReadByTheDialectOfTheSettingsFile() =>
        DialectQueries.ByProject.ShouldBe("SELECT E'it'\n    '\\'s -- not a comment' AS note;");

    // The header of ByHeader.sql says mysql.  That the file is read as MySQL shows the header over the settings file.
    [Fact]
    public void ProjectWithADialect_FileWithAHeader_IsReadByTheDialectOfItsHeader() =>
        DialectQueries.ByHeader.ShouldBe("SELECT 'it\\'s' AS note, 5--3 AS eight;");

    // The header of ByOption.sql names an option after the dialect, with a comma.  Plain MySQL would not close the
    // string, so this project would not build.
    [Fact]
    public void ProjectWithADialect_FileWithAnOptionInItsHeader_IsReadByThatOption() =>
        DialectQueries.ByOption.ShouldBe("SELECT 'C:\\temp\\' AS path;");

    [Fact]
    public void ProjectWithADialect_FileWithAMarker_IsReadByTheDialectItNames() =>
        DialectQueries.ByMarker.ShouldBe("SELECT [it's] FROM #orders;");

    // The header of Kept.sql lists no-token-validation and keep-comments as the generator parameters.  That the comment
    // is there shows the header reaching the generator and replacing the settings file's list.
    [Fact]
    public void ProjectWithParameters_FileWithAHeader_UsesTheListOfItsHeader() =>
        ParameterQueries.Kept.ShouldBe("SELECT 1 /* kept by the header */ AS one;");

    [Fact]
    public void ProjectWithParameters_TwoTypesClaimOneFile_EachUsesItsOwnAttribute()
    {
        ParameterQueries.Shared.ShouldBe("SELECT 2   AS two;");
        KeptQueries.Shared.ShouldBe("SELECT 2 /* kept by the attribute */ AS two;");
    }

    [Fact]
    public void Method_Call_AllocatesTheStringItReturnsAndNothingElse()
    {
        // The first call creates the delegate that the method then keeps.
        var expected = TokenQueries.Search("id, name", "users", "name LIKE @pattern");

        var start = GC.GetAllocatedBytesForCurrentThread();
        var sql = TokenQueries.Search("id, name", "users", "name LIKE @pattern");
        var afterCall = GC.GetAllocatedBytesForCurrentThread();
        var sameLength = new string('x', sql.Length);
        var afterString = GC.GetAllocatedBytesForCurrentThread();

        sql.ShouldBe(expected);
        sameLength.Length.ShouldBe(sql.Length);
        (afterCall - start).ShouldBe(afterString - afterCall);
    }

    [Fact]
    public void GenericType_PathToAFolder_SharesTheFileWithAnotherType() =>
        new Repository<int>(7).Describe().ShouldBe(OrderQueries.GetOrder + " -- 7");

    [Fact]
    public void NestedRecordStruct_PathThroughAParentFolder_HasTheConstant() =>
        Outer.Counts.CountUsers.ShouldBe("SELECT COUNT(*) FROM users;");

    [Theory]
    [InlineData(typeof(UserQueries))]
    [InlineData(typeof(OrderQueries))]
    [InlineData(typeof(Repository<>))]
    [InlineData(typeof(Outer.Counts))]
    [InlineData(typeof(TokenQueries))]
    [InlineData(typeof(DialectQueries))]
    [InlineData(typeof(ParameterQueries))]
    [InlineData(typeof(KeptQueries))]
    public void Attribute_OnAGeneratedType_IsNotInItsMetadata(Type type) =>
        type.GetCustomAttributesData()
            .Select(attribute => attribute.AttributeType.FullName)
            .ShouldNotContain("SqlSource.SqlSourceGenerateAttribute");

    [Fact]
    public void AttributeAndEnum_OfTheConsumingAssembly_AreInternalTypes()
    {
        var assembly = typeof(EndToEndTests).Assembly;

        AttributeSource.GeneratedTypes.ShouldNotBeEmpty();
        foreach (var name in AttributeSource.GeneratedTypes)
        {
            assembly.GetType(name).ShouldNotBeNull().IsNotPublic.ShouldBeTrue();
        }
    }
}
