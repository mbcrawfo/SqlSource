using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using SqlSource;

namespace SqlServerExamples.Models;

// The output models: each query gets the SQL and its models, read here through Dapper.  The input model is the
// parameter object, and the output model the type of a row.
[SqlSourceGenerate(Output = GeneratorOutput.Models)]
public sealed partial class StoreQueries(DbConnection connection)
{
    // A record of the query's columns.  ShippedOn is a DateOnly?, since the column is a date that can be null, and the
    // project's type override makes a date a DateOnly.
    public Task<GetOrderDto?> GetOrderAsync(int orderId) =>
        connection.QuerySingleOrDefaultAsync<GetOrderDto>(Sql.GetOrder, new GetOrderParams(orderId));

    // Since is a datetime2, which Dapper would send as a datetime, so it goes as a TypedParameter, and the parameters
    // are given by hand.  Count is an int by the query's -- param: marker, where the server would infer a bigint.
    public Task<IEnumerable<ListOrdersPlacedSinceDto>> ListOrdersPlacedSinceAsync(int count, DateTime since) =>
        connection.QueryAsync<ListOrdersPlacedSinceDto>(
            Sql.ListOrdersPlacedSince,
            new { count, since = new TypedParameter(SqlDbType.DateTime2, since) }
        );

    // Shipped is an int and not an int?, by the query's -- column: marker, which leaves the column's name alone for
    // Dapper: a count is never null, though the server calls it nullable.
    public Task<IEnumerable<CountShippedOrdersDto>> CountShippedOrdersAsync() =>
        connection.QueryAsync<CountShippedOrdersDto>(Sql.CountShippedOrders);

    // The OUTPUT clause gives the rows of the statement: the order as the database filled it in.
    public Task<AddOrderDto> AddOrderAsync(int userId) =>
        connection.QuerySingleAsync<AddOrderDto>(Sql.AddOrder, new AddOrderParams(userId));

    // SELECT * gives the model of the table, UsersDto, which every query that reads the whole table shares.
    public Task<UsersDto?> GetUserAsync(int userId) =>
        connection.QuerySingleOrDefaultAsync<UsersDto>(Sql.GetUser, new GetUserParams(userId));
}
