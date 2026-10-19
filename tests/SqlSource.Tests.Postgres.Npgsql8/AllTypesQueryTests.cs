using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using NpgsqlTypes;
using Shouldly;
using SqlSource.Testing.EndToEnd;
using Xunit;

namespace SqlSource.Tests.Postgres.Npgsql8;

// The type map on Npgsql 8, the oldest driver that is supported: the models the generator gives under it, and both rows
// of all_types read through Dapper into AllTypesDto.  The end-to-end project proves the same table on Npgsql 10.
public sealed class AllTypesQueryTests(AppContainer app) : IClassFixture<AppContainer>
{
    [Fact]
    public void Npgsql_Loaded_IsVersion8() => typeof(NpgsqlConnection).Assembly.GetName().Version!.Major.ShouldBe(8);

    // The types that differ from Npgsql 10's: date and time are a DateTime and a TimeSpan, cidr is an NpgsqlCidr, and a
    // range of dates is a range of DateTime.
    [Fact]
    public void Models_AllTypes_HasNpgsql8sTypes()
    {
        var properties = typeof(AllTypesDto)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(static property => property.Name, static property => property.PropertyType);

        properties["DateValue"].ShouldBe(typeof(DateTime?));
        properties["TimeValue"].ShouldBe(typeof(TimeSpan?));
        properties["CidrValue"].ShouldBe(typeof(NpgsqlCidr?));
        properties["DaterangeValue"].ShouldBe(typeof(NpgsqlRange<DateTime>?));
        properties["DatemultirangeValue"].ShouldBe(typeof(NpgsqlRange<DateTime>[]));
    }

    [Fact]
    public async Task QueryAsync_ListAllTypes_ReadsBothRows()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        await using var source = NpgsqlDataSource.Create(app.ConnectionString);
        await using var connection = await source.OpenConnectionAsync(TestContext.Current.CancellationToken);

        var rows = (await connection.QueryAsync<AllTypesDto>(AllTypesQueries.ListAllTypes)).ToList();

        rows.Count.ShouldBe(2);
        AllTypesRows.ShouldBeTheFullRow(rows[0]);
        AllTypesRows.ShouldBeTheNullRow(rows[1]);
    }
}
