using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using SqlSource;

namespace SqlServerExamples.CodeGen;

// The default output, codegen, which needs nothing on the attribute.  Each query gets its models and a method that
// runs it, an extension method of the connection in the generated class StoreQueriesExtensions, which opens the
// connection when it is closed, sends the input model's values and reads the rows into the output model.  Each method
// here passes on the caller's CancellationToken.  See docs/manual/generator.md, "Query methods".
[SqlSourceGenerate]
public sealed partial class StoreQueries(DbConnection connection)
{
    // many, the default for a query with rows, returns an array.  The parameter and the column OrderId are OrderIds,
    // by the query's marker and by the project's column types.
    public Task<ListOrderLinesDto[]> ListOrderLinesAsync(
        OrderId orderId,
        CancellationToken cancellationToken = default
    ) => connection.ListOrderLines(new ListOrderLinesParams(orderId), cancellationToken: cancellationToken);

    // LatestOrderId is an OrderId by the query's -- column: marker, since the column types reach no computed column.
    public Task<ListLatestOrderPerProductDto[]> ListLatestOrderPerProductAsync(
        CancellationToken cancellationToken = default
    ) => connection.ListLatestOrderPerProduct(cancellationToken: cancellationToken);

    // one-optional returns the row, or null.  The parameter and the column Email are EmailAddresses, by the file's
    // type override.
    public Task<FindUserByEmailDto?> FindUserByEmailAsync(
        EmailAddress email,
        CancellationToken cancellationToken = default
    ) => connection.FindUserByEmail(new FindUserByEmailParams(email), cancellationToken: cancellationToken);

    // rowcount returns the count of rows updated.
    public Task<int> ChangeUserEmailAsync(
        int userId,
        EmailAddress email,
        CancellationToken cancellationToken = default
    ) => connection.ChangeUserEmail(new ChangeUserEmailParams(email, userId), cancellationToken: cancellationToken);

    // The query's -- collection-type: marker makes its method return an IReadOnlyList.
    public Task<IReadOnlyList<ListUsersNewestFirstDto>> ListUsersNewestFirstAsync(
        CancellationToken cancellationToken = default
    ) => connection.ListUsersNewestFirst(cancellationToken: cancellationToken);

    // A token is a string argument of the method, after the input model when the query has one.  Pass only text your
    // own code chooses, such as "created_at DESC".
    public Task<ListUsersSortedByDto[]> ListUsersSortedByAsync(
        string orderBy,
        CancellationToken cancellationToken = default
    ) => connection.ListUsersSortedBy(orderBy, cancellationToken: cancellationToken);
}
