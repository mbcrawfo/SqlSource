using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using SqlSource;

namespace PostgresExamples.Models;

// The output models: each query gets the SQL and its models, read here through Dapper.  The input model is the
// parameter object, and the output model the type of a row.
[SqlSourceGenerate(Output = GeneratorOutput.Models)]
public sealed partial class StoreQueries(DbConnection connection)
{
    // A record of the query's columns.  ShippedAt is nullable, since the column is, and PlacedAt a DateTimeOffset by
    // the project's type override.
    public Task<GetOrderDto?> GetOrderAsync(int orderId) =>
        connection.QuerySingleOrDefaultAsync<GetOrderDto>(Sql.GetOrder, new GetOrderParams(orderId));

    // Total is a decimal and not a decimal?, by the query's -- column: marker, which leaves the column's name alone for
    // Dapper.
    public Task<IEnumerable<ListOrderTotalsDto>> ListOrderTotalsAsync() =>
        connection.QueryAsync<ListOrderTotalsDto>(Sql.ListOrderTotals);

    // GetOrderTagsDto is a record with an array column, which Dapper reads as any other.
    public Task<GetOrderTagsDto?> GetOrderTagsAsync(int orderId) =>
        connection.QuerySingleOrDefaultAsync<GetOrderTagsDto>(Sql.GetOrderTags, new GetOrderTagsParams(orderId));

    // SELECT * gives the model of the table, UsersDto, which every query that reads the whole table shares.
    public Task<UsersDto?> GetUserAsync(int userId) =>
        connection.QuerySingleOrDefaultAsync<UsersDto>(Sql.GetUser, new GetUserParams(userId));
}
