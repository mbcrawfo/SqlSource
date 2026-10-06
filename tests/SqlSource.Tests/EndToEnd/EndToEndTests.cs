using System;
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
    public void QueryWithTokens_GetsNoMember() =>
        typeof(OrderQueries)
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Select(field => field.Name)
            .ShouldBe(["GetOrder"]);

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
