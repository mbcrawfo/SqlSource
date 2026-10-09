using System;
using System.Collections.Generic;
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

    // SqlSource.Tests.csproj sets SqlSourceGeneratorParameters to no-token-validation, on a line of its own, and
    // Search has no list of its own.  That its method checks nothing shows the property reaching the generator
    // through the MSBuild files the package ships, trimmed.
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

    // The item of ByOption.sql names an option after the dialect, with a comma and a space, over several lines.
    // Plain MySQL would not close the string, and this project would not build.
    [Fact]
    public void ProjectWithADialect_FileWithAnOptionInItsMetadata_IsReadByThatOption() =>
        DialectQueries.ByOption.ShouldBe("SELECT 'C:\\temp\\' AS path;");

    // The build copies the file it wrote for the compiler of this project to the output folder.  It has a section for
    // each item whose metadata the package shows to the compiler.  A section for every .sql file would make the file,
    // and the build, grow with files that set nothing.  A value with an option arrives whole, comma included.
    [Fact]
    public void ProjectWithMetadata_FileTheBuildWritesForTheCompiler_NamesOnlyTheFilesWithMetadata()
    {
        var lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "build", "compiler.editorconfig"));

        var values = new List<string>();
        var section = string.Empty;
        foreach (var line in lines)
        {
            if (line.StartsWith('['))
            {
                section = line.EndsWith(".sql]", StringComparison.Ordinal)
                    ? line[line.LastIndexOf('/')..]
                    : string.Empty;
            }
            else if (
                section.Length > 0
                && line.Contains(" = ", StringComparison.Ordinal)
                && !line.EndsWith(" = ", StringComparison.Ordinal)
            )
            {
                values.Add(section + " " + line);
            }
        }

        values.ShouldBe(
            [
                "/ByMetadata.sql] build_metadata.SqlSourceSettingsFile.SqlSourceDialect = mysql",
                "/ByOption.sql] build_metadata.SqlSourceSettingsFile.SqlSourceDialect = mysql, no-backslash-escapes",
                "/Kept.sql] build_metadata.SqlSourceSettingsFile.SqlSourceGeneratorParameters = keep-comments",
            ],
            ignoreOrder: true
        );
    }

    [Fact]
    public void ProjectWithADialect_FileWithAMarker_IsReadByTheDialectItNames() =>
        DialectQueries.ByMarker.ShouldBe("SELECT [it's] FROM #orders;");

    // The item of Kept.sql has SqlSourceGeneratorParameters metadata, written over several lines.  That the comment
    // is there shows the metadata reaching the generator through the MSBuild files the package ships, trimmed, and
    // replacing the project's list.
    [Fact]
    public void ProjectWithParameters_FileWithMetadata_UsesTheListOfItsItem() =>
        ParameterQueries.Kept.ShouldBe("SELECT 1 /* kept by the metadata */ AS one;");

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
    public void Attribute_IsNotInTheMetadataOfTheTypesThatCarryIt(Type type) =>
        type.GetCustomAttributesData()
            .Select(attribute => attribute.AttributeType.FullName)
            .ShouldNotContain("SqlSource.SqlSourceGenerateAttribute");

    [Fact]
    public void AttributeAndEnum_AreInternalTypesOfTheConsumingAssembly()
    {
        var assembly = typeof(EndToEndTests).Assembly;

        assembly.GetType("SqlSource.SqlSourceGenerateAttribute").ShouldNotBeNull().IsNotPublic.ShouldBeTrue();
        assembly.GetType("SqlSource.SqlLocation").ShouldNotBeNull().IsNotPublic.ShouldBeTrue();
    }
}
