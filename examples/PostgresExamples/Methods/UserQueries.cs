using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SqlSource;

namespace PostgresExamples.Methods;

// The queries of UserQueries.sql, the file named for the class.  Their methods are in the generated class
// UserQueriesExtensions.
[SqlSourceGenerate]
public sealed partial class UserQueries(DbConnection connection)
{
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

    // The query's -- collection-type: IAsyncEnumerable makes its method return an IAsyncEnumerable, whose rows the loop
    // takes one at a time, as each is read.  Nothing runs until the loop starts, and the connection is busy until it
    // ends, so the loop's body runs no other query on it.
    public async Task WriteUserNamesAsync(TextWriter writer, CancellationToken cancellationToken = default)
    {
        await foreach (
            var user in connection.StreamUsersOldestFirst(cancellationToken: cancellationToken).ConfigureAwait(false)
        )
        {
            await writer.WriteLineAsync(user.Name).ConfigureAwait(false);
        }
    }

    // A token is a string argument of the method, after the input model when the query has one.  Pass only text your
    // own code chooses, such as "created_at DESC".
    public Task<ListUsersSortedByDto[]> ListUsersSortedByAsync(
        string orderBy,
        CancellationToken cancellationToken = default
    ) => connection.ListUsersSortedBy(orderBy, cancellationToken: cancellationToken);
}
