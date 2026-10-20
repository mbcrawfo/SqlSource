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

    // Total is a decimal and not a decimal?: the sum of a group of lines whose columns are NOT NULL is never null, and
    // the tool shows it.
    public Task<IEnumerable<ListOrderTotalsDto>> ListOrderTotalsAsync() =>
        connection.QueryAsync<ListOrderTotalsDto>(Sql.ListOrderTotals);

    // Shipped is a long and not a long?, by the query's -- column: marker, which leaves the column's name alone for
    // Dapper: a count is never null, though the tool cannot show it for count(o.shipped_at).
    public Task<IEnumerable<CountShippedOrdersDto>> CountShippedOrdersAsync() =>
        connection.QueryAsync<CountShippedOrdersDto>(Sql.CountShippedOrders);

    // A class and not a record, by the query's -- output-model-type: marker: Dapper cannot construct a record from a
    // row with an array.
    public Task<GetOrderTagsDto?> GetOrderTagsAsync(int orderId) =>
        connection.QuerySingleOrDefaultAsync<GetOrderTagsDto>(Sql.GetOrderTags, new GetOrderTagsParams(orderId));

    // SELECT * gives the model of the table, UsersDto, which every query that reads the whole table shares.
    public Task<UsersDto?> GetUserAsync(int userId) =>
        connection.QuerySingleOrDefaultAsync<UsersDto>(Sql.GetUser, new GetUserParams(userId));
}
