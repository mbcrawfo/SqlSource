using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using SqlSource;

namespace PostgresExamples.CodeGen;

// The queries of OrderQueries.sql, the file named for the class, whose markers put their methods on the class itself:
// ListOrdersOfUser and FindOrder are public static methods, which any code calls as
// OrderQueries.ListOrdersOfUser(connection, ...), and CountOrderLines is a method of the private nested class Sql,
// which only this class's own code calls.  See docs/manual/settings.md#setting-member-location.
[SqlSourceGenerate]
public static partial class OrderQueries
{
    // A method of your own beside the generated ones, which calls the nested class's method.
    public static async Task<bool> HasLinesAsync(
        DbConnection connection,
        int orderId,
        CancellationToken cancellationToken = default
    )
    {
        var lines = await Sql.CountOrderLines(
                connection,
                new CountOrderLinesParams(orderId),
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
        return lines.LineCount > 0;
    }
}
