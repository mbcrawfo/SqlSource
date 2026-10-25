using System.Data.Common;
using System.Threading.Tasks;
using SqlSource;

namespace PostgresExamples.CodeGen;

// The default output, codegen, which needs nothing on the attribute.  Each query gets its models and a method that
// runs it: ListOrderLines and ListLatestOrderPerProduct, extension methods of the connection in the generated class
// OrderLineQueriesExtensions, which bind the input model's values and read the rows into the output model.
[SqlSourceGenerate]
public sealed partial class OrderLineQueries(DbConnection connection)
{
    // The parameter and the column OrderId are OrderIds, by the query's marker and by the project's column types.
    public Task<ListOrderLinesDto[]> ListOrderLinesAsync(OrderId orderId) =>
        connection.ListOrderLines(new ListOrderLinesParams(orderId));

    // LatestOrderId is an OrderId by the query's -- column: marker, since the column types reach no computed column.
    public Task<ListLatestOrderPerProductDto[]> ListLatestOrderPerProductAsync() =>
        connection.ListLatestOrderPerProduct();
}
