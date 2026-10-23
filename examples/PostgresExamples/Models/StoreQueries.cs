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

    // Shipped is a long and not a long?: a count is never null, even of a column that may be, and the tool shows it.
    public Task<IEnumerable<CountShippedOrdersDto>> CountShippedOrdersAsync() =>
        connection.QueryAsync<CountShippedOrdersDto>(Sql.CountShippedOrders);

    // DisplayName is a string and not a string?, by the query's -- column: marker, which leaves the column's name
    // alone for Dapper: initcap of a name that is never null is not null, though the tool cannot show it.
    public Task<IEnumerable<ListUserDisplayNamesDto>> ListUserDisplayNamesAsync() =>
        connection.QueryAsync<ListUserDisplayNamesDto>(Sql.ListUserDisplayNames);

    // GetOrderTagsDto is a record with an array column, which Dapper reads as any other.
    public Task<GetOrderTagsDto?> GetOrderTagsAsync(int orderId) =>
        connection.QuerySingleOrDefaultAsync<GetOrderTagsDto>(Sql.GetOrderTags, new GetOrderTagsParams(orderId));

    // SELECT * gives the model of the table, UsersDto, which every query that reads the whole table shares.
    public Task<UsersDto?> GetUserAsync(int userId) =>
        connection.QuerySingleOrDefaultAsync<UsersDto>(Sql.GetUser, new GetUserParams(userId));
}
