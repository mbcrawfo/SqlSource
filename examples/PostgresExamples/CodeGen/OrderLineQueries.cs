using System.Data.Common;
using System.Threading.Tasks;
using SqlSource;

namespace PostgresExamples.CodeGen;

// The default output, codegen, which needs nothing on the attribute.  Each query gets its models and a method that
// runs it: ListOrderLines, an extension method of the connection in the generated class OrderLineQueriesExtensions,
// which binds the input model's values and reads the rows into the output model.
[SqlSourceGenerate]
public sealed partial class OrderLineQueries(DbConnection connection)
{
    public Task<ListOrderLinesDto[]> ListOrderLinesAsync(int orderId) =>
        connection.ListOrderLines(new ListOrderLinesParams(orderId));
}
