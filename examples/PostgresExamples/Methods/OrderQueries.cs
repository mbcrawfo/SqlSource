using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using SqlSource;

namespace PostgresExamples.Methods;

// The queries of OrderQueries.sql, the file named for the class, whose markers put their methods on the class itself:
// ListOrdersOfUser, FindOrder and ListOrdersByStatuses are public static methods, which any code calls as
// OrderQueries.ListOrdersOfUserAsync(connection, ...), and CountOrderLines is a method of the private nested class Sql,
// which only this class's own code calls.  The header's async-method-suffix puts Async at the end of each method's
// name.  See docs/manual/settings.md#setting-member-location and
// docs/manual/settings.md#setting-generator-parameters.
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
        var lines = await Sql.CountOrderLinesAsync(
                connection,
                new CountOrderLinesParams(orderId),
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
        return lines.LineCount > 0;
    }

    // A method of your own that passes a list: ListOrdersByStatusesParams.Statuses is an IEnumerable<string>?, which
    // takes this array, a List<string> or any other sequence, and the method sends one parameter for each value.  See
    // docs/manual/parameters-and-tokens.md#list-parameters.
    public static Task<ListOrdersByStatusesDto[]> ListOrdersNotCancelledAsync(
        DbConnection connection,
        CancellationToken cancellationToken = default
    )
    {
        string[] notCancelled = ["pending", "shipped"];
        return ListOrdersByStatusesAsync(
            connection,
            new ListOrdersByStatusesParams(notCancelled),
            cancellationToken: cancellationToken
        );
    }
}
