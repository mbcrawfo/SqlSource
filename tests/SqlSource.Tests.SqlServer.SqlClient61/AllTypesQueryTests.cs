using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlTypes;
using Shouldly;
using SqlSource.Testing.EndToEnd;
using Xunit;

namespace SqlSource.Tests.SqlServer.SqlClient61;

// The type map on SqlClient 6.1, the oldest driver that is supported: the models the generator gives under it, and
// both rows of all_types and of new_types read through Dapper.  The end-to-end project proves the same tables on
// SqlClient 7.
public sealed class AllTypesQueryTests(AppContainer app) : IClassFixture<AppContainer>
{
    // Dapper's settings are the process's, so the snake_case columns are matched from before the first test.  The
    // queries give date back to the type map, a DateTime, so that no handler is needed.
    static AllTypesQueryTests()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    // The driver is the one the project asks for, 6.1.x, and not the 7 that the end-to-end project gets.
    [Fact]
    public void SqlClient_Loaded_IsVersion61()
    {
        var version = typeof(SqlConnection).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        version.ShouldNotBeNull().InformationalVersion.ShouldStartWith("6.1.");
    }

    // The types are those of the end-to-end project on SqlClient 7: nothing in the type map differs between the two.
    [Fact]
    public void Build_AllTypes_HasTheSameTypesAsSqlClient7()
    {
        PropertiesOf(typeof(AllTypesDto))
            .ShouldBe([
                ("Id", typeof(int)),
                ("BigintValue", typeof(long?)),
                ("IntValue", typeof(int?)),
                ("SmallintValue", typeof(short?)),
                ("TinyintValue", typeof(byte?)),
                ("BitValue", typeof(bool?)),
                ("DecimalValue", typeof(decimal?)),
                ("NumericValue", typeof(decimal?)),
                ("MoneyValue", typeof(decimal?)),
                ("SmallmoneyValue", typeof(decimal?)),
                ("FloatValue", typeof(double?)),
                ("RealValue", typeof(float?)),
                ("DateValue", typeof(DateTime?)),
                ("DatetimeValue", typeof(DateTime?)),
                ("Datetime2Value", typeof(DateTime?)),
                ("SmalldatetimeValue", typeof(DateTime?)),
                ("TimeValue", typeof(TimeSpan?)),
                ("DatetimeoffsetValue", typeof(DateTimeOffset?)),
                ("CharValue", typeof(string)),
                ("VarcharValue", typeof(string)),
                ("TextValue", typeof(string)),
                ("NcharValue", typeof(string)),
                ("NvarcharValue", typeof(string)),
                ("NtextValue", typeof(string)),
                ("XmlValue", typeof(string)),
                ("BinaryValue", typeof(byte[])),
                ("VarbinaryValue", typeof(byte[])),
                ("ImageValue", typeof(byte[])),
                ("RowversionValue", typeof(byte[])),
                ("UniqueidentifierValue", typeof(Guid?)),
                ("SqlVariantValue", typeof(object)),
                ("SysnameValue", typeof(string)),
            ]);
        PropertiesOf(typeof(NewTypesDto))
            .ShouldBe([("Id", typeof(int)), ("JsonValue", typeof(string)), ("VectorValue", typeof(SqlVector<float>?))]);
    }

    [Fact]
    public async Task QueryAsync_ListAllTypes_ReadsBothRows()
    {
        await using var source = SqlClientFactory.Instance.CreateDataSource(app.ConnectionString);
        await using var connection = await source.OpenConnectionAsync(TestContext.Current.CancellationToken);

        var rows = (await connection.QueryAsync<AllTypesDto>(AllTypesQueries.ListAllTypes)).ToList();

        rows.Count.ShouldBe(2);
        SqlServerAllTypesRows.ShouldBeTheFullRow(rows[0]);
        SqlServerAllTypesRows.ShouldBeTheNullRow(rows[1]);
    }

    [Fact]
    public async Task QueryAsync_ListNewTypes_ReadsBothRows()
    {
        await using var source = SqlClientFactory.Instance.CreateDataSource(app.ConnectionString);
        await using var connection = await source.OpenConnectionAsync(TestContext.Current.CancellationToken);

        var rows = (await connection.QueryAsync<NewTypesDto>(NewTypeQueries.ListNewTypes)).ToList();

        rows.Count.ShouldBe(2);
        SqlServerAllTypesRows.ShouldBeTheFullNewTypesRow(rows[0]);
        SqlServerAllTypesRows.ShouldBeTheNullNewTypesRow(rows[1]);
    }

    // The public properties in the order they are declared, each with its type.
    private static (string Name, Type Type)[] PropertiesOf(Type model) =>
        [
            .. model
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(static property => (property.Name, property.PropertyType)),
        ];
}
