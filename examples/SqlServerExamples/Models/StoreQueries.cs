using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using SqlSource;

namespace SqlServerExamples.Models;

// The output models, which the folder's sqlsource.json sets: each query gets the SQL and its models, read here through
// Dapper.  The input model is the parameter object, and the output model the type of a row.  Source = "." gives the
// class every .sql file of this folder, Orders.sql and Users.sql, in place of the one file named for it.
[SqlSourceGenerate(Source = ".")]
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

    // Shipped is an int and not an int?: a count is never null, even of a column that may be, and the tool shows it.
    public Task<IEnumerable<CountShippedOrdersDto>> CountShippedOrdersAsync() =>
        connection.QueryAsync<CountShippedOrdersDto>(Sql.CountShippedOrders);

    // ShippedOn is a DateOnly and not a DateOnly?: the column can hold null, and the WHERE keeps those rows out.
    public Task<IEnumerable<ListShippedOrdersDto>> ListShippedOrdersAsync() =>
        connection.QueryAsync<ListShippedOrdersDto>(Sql.ListShippedOrders);

    // UserId is an int and not an int?: each row of the FULL JOIN has a user or an order, and COALESCE takes the id
    // from whichever it has, so the tool shows it is never null.  Name and OrderId are nullable, since either side
    // can be missing.
    public Task<IEnumerable<ListUsersAndOrdersDto>> ListUsersAndOrdersAsync() =>
        connection.QueryAsync<ListUsersAndOrdersDto>(Sql.ListUsersAndOrders);

    // LineTotal is a decimal and not a decimal?: SQL Server says a computed column that is not persisted can be null,
    // but quantity * unit_price of columns that are never null is not, and an inner join keeps every line, so the
    // tool shows it.
    public Task<IEnumerable<ListOrderLineTotalsDto>> ListOrderLineTotalsAsync() =>
        connection.QueryAsync<ListOrderLineTotalsDto>(Sql.ListOrderLineTotals);

    // MonthStart is a DateOnly and not a DateOnly?, by the query's -- column: marker, which leaves the column's name
    // alone for Dapper: DATEFROMPARTS of parts that are never null is not null, though the tool cannot show it.
    public Task<IEnumerable<ListOrderMonthsDto>> ListOrderMonthsAsync() =>
        connection.QueryAsync<ListOrderMonthsDto>(Sql.ListOrderMonths);

    // CalendarDay and DayNumber are a DateOnly and an int, not nullable: the recursive WITH query starts each order at
    // a day that is never null and adds a day to it, so the tool shows that no row holds a null.
    public Task<IEnumerable<ListShippingDaysDto>> ListShippingDaysAsync() =>
        connection.QueryAsync<ListShippingDaysDto>(Sql.ListShippingDays);

    // The OUTPUT clause gives the rows of the statement: the order as the database filled it in.
    public Task<AddOrderDto> AddOrderAsync(int userId) =>
        connection.QuerySingleAsync<AddOrderDto>(Sql.AddOrder, new AddOrderParams(userId));

    // ShippedOn is a DateOnly and not a DateOnly?: the WHERE keeps the orders with no day out and the SET does not
    // write the column, so the tool shows that the OUTPUT clause's day is never null.
    public Task<IEnumerable<MarkOrdersShippedDto>> MarkOrdersShippedAsync() =>
        connection.QueryAsync<MarkOrdersShippedDto>(Sql.MarkOrdersShipped);

    // SELECT * gives the model of the table, UsersDto, which every query that reads the whole table shares.
    public Task<UsersDto?> GetUserAsync(int userId) =>
        connection.QuerySingleOrDefaultAsync<UsersDto>(Sql.GetUser, new GetUserParams(userId));
}
