using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.EndToEnd;

// The types of this folder are compiled by the build with the generator loaded, as a consumer's would be, and get
// their .sql files through the MSBuild file that the package ships.
public class EndToEndTests
{
    [Fact]
    public void NestedMode_DefaultFolder_HasAConstantForEachQueryOfEachFile()
    {
        UserQueries.GetUserSql.ShouldBe("SELECT id, name\nFROM users\nWHERE id = @id;");
        UserQueries.ListUsersSql.ShouldBe("SELECT id, name\nFROM users\nORDER BY name;");
        UserQueries.CountUsersSql.ShouldBe("SELECT COUNT(*) FROM users;");
    }

    [Fact]
    public void NestedMode_SqlClass_IsPrivateStaticAndHoldsPublicConstants()
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
    public void DirectMode_PathToAFile_PutsTheConstantsOnTheType()
    {
        // Evaluated by the compiler: the member is a constant.
        const string Sql = OrderQueries.GetOrder;

        Sql.ShouldBe("SELECT id, total /* in cents */\nFROM orders\nWHERE id = @id;");
    }

    [Fact]
    public void DirectMode_QueryWithTokens_IsAMethodThatReplacesEachOccurrence() =>
        OrderQueries
            .OrdersOf("sales", "u.name = @name")
            .ShouldBe(
                "-- The schema is a token, so this query is a method.\n"
                    + "SELECT o.id FROM sales.orders AS o INNER JOIN sales.users AS u ON u.id = o.user_id "
                    + "WHERE u.name = @name;"
            );

    [Fact]
    public void NestedMode_QueryWithSeveralTokens_ReplacesEachOccurrence() =>
        TokenQueries
            .Search("id, name", "users", "name LIKE @pattern")
            .ShouldBe("SELECT id, name\nFROM users\nWHERE name LIKE @pattern\nORDER BY users.id;");

    // SqlSource.Tests.csproj sets SqlSourceTokenValidation to false, and Search has no directive.  That its method
    // checks nothing shows the property reaching the generator through the MSBuild file the package ships.
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void ProjectWithValidationOff_QueryWithoutADirective_AcceptsAnArgumentWithoutText(string filter) =>
        TokenQueries
            .Search("id", "users", filter)
            .ShouldBe("SELECT id\nFROM users\nWHERE " + filter + "\nORDER BY users.id;");

    [Fact]
    public void ProjectWithValidationOff_QueryWithoutADirective_FailsOnANullArgumentWhereItIsRead() =>
        Should.Throw<NullReferenceException>(() => TokenQueries.Search("id", null!, "1 = 1"));

    [Theory]
    [InlineData("")]
    [InlineData(" \t")]
    public void ProjectWithValidationOff_QueryWithTheDirective_RejectsAnArgumentWithoutText(string filter) =>
        Should.Throw<ArgumentException>(() => TokenQueries.Checked("users", filter)).ParamName.ShouldBe("filter");

    [Fact]
    public void ProjectWithValidationOff_QueryWithTheDirective_RejectsANullArgument() =>
        Should.Throw<ArgumentNullException>(() => TokenQueries.Checked(null!, "1 = 1")).ParamName.ShouldBe("table");

    [Fact]
    public void ProjectWithValidationOff_QueryWithTheDirective_ReturnsTheSqlForArgumentsWithText() =>
        TokenQueries.Checked("users", "id = @id").ShouldBe("SELECT id FROM users WHERE id = @id;");

    // SqlSource.Tests.csproj sets the SqlSourceDialect property to postgres, on a line of its own.  ANSI would end the
    // string at the quote after the backslash and take the rest of the line for a comment.
    [Fact]
    public void ProjectWithADialect_FileWithoutItsOwn_IsReadByTheDialectOfTheProject() =>
        DialectQueries.ByProperty.ShouldBe("SELECT E'it'\n    '\\'s -- not a comment' AS note;");

    // The item of ByMetadata.sql has SqlSourceDialect metadata, written over several lines.  That the file is read as
    // MySQL shows the metadata reaching the generator through the MSBuild files the package ships, trimmed.
    [Fact]
    public void ProjectWithADialect_FileWithMetadata_IsReadByTheDialectOfItsItem() =>
        DialectQueries.ByMetadata.ShouldBe("SELECT 'it\\'s' AS note, 5--3 AS eight;");

    // The build copies the file it wrote for the compiler of this project to the output folder.  It has a section for
    // each item whose metadata the package shows to the compiler.  A section for every .sql file would make the file,
    // and the build, grow with files that set nothing.
    [Fact]
    public void ProjectWithADialect_FileTheBuildWritesForTheCompiler_NamesOnlyTheFileWithMetadata()
    {
        var lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "build", "compiler.editorconfig"));

        var sections = lines.Where(line => line.StartsWith('[') && line.EndsWith(".sql]", StringComparison.Ordinal));

        var section = sections.ShouldHaveSingleItem();
        section.ShouldEndWith("/EndToEnd/Dialects/ByMetadata.sql]");
        lines[Array.IndexOf(lines, section) + 1]
            .ShouldBe("build_metadata.SqlSourceDialectFile.SqlSourceDialect = mysql");
    }

    [Fact]
    public void ProjectWithADialect_FileWithADirective_IsReadByTheDialectItNames() =>
        DialectQueries.ByDirective.ShouldBe("SELECT [it's] FROM #orders;");

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
    public void Attribute_IsNotInTheMetadataOfTheTypesThatCarryIt(Type type) =>
        type.GetCustomAttributesData()
            .Select(attribute => attribute.AttributeType.FullName)
            .ShouldNotContain("SqlSource.SqlQueriesAttribute");

    [Fact]
    public void AttributeAndEnum_AreInternalTypesOfTheConsumingAssembly()
    {
        var assembly = typeof(EndToEndTests).Assembly;

        assembly.GetType("SqlSource.SqlQueriesAttribute").ShouldNotBeNull().IsNotPublic.ShouldBeTrue();
        assembly.GetType("SqlSource.SqlQueriesMode").ShouldNotBeNull().IsNotPublic.ShouldBeTrue();
    }
}
