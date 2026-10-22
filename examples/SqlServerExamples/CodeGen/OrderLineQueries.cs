using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using SqlSource;

namespace SqlServerExamples.CodeGen;

// The default output, codegen, which needs nothing on the attribute.  It gives the SQL and the models today; the
// method below is what a generated method that runs the query will replace.
[SqlSourceGenerate]
public sealed partial class OrderLineQueries(DbConnection connection)
{
    public Task<IEnumerable<ListOrderLinesDto>> ListOrderLinesAsync(int orderId) =>
        connection.QueryAsync<ListOrderLinesDto>(Sql.ListOrderLines, new ListOrderLinesParams(orderId));
}
