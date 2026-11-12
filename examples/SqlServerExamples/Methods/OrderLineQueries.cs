using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using SqlSource;

namespace SqlServerExamples.Methods;

// The output methods, which the folder's sqlsource.json sets, and the default file, OrderLineQueries.sql beside this
// one, which needs nothing on the attribute.  Each query gets its models and a method that runs it, an extension method
// of the connection in the generated class OrderLineQueriesExtensions, which opens the connection when it is closed,
// sends the input model's values and reads the rows into the output model.  Each method here passes on the caller's
// CancellationToken.  See site/generator.md, "Query methods".
[SqlSourceGenerate]
public sealed partial class OrderLineQueries(DbConnection connection)
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
}
