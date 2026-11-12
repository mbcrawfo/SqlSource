using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SqlSource;

namespace SqlServerExamples.Methods;

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

    // one returns the row of the query's OUTPUT clause, which holds the new user's id.
    public async Task<int> AddUserAsync(
        string name,
        EmailAddress email,
        CancellationToken cancellationToken = default
    ) =>
        (
            await connection
                .AddUser(new AddUserParams(name, email), cancellationToken: cancellationToken)
                .ConfigureAwait(false)
        ).Id;

    // The query's -- generator: collection-method gives AddUser a second method, which takes every user's input
    // model and adds them all in one transaction of its own, sending up to 1,000 in each round trip.  It returns their
    // rows in the order of the users, and a user the database refuses throws CollectionItemException, whose ItemIndex
    // is its position, after the transaction has been rolled back.
    public async Task<int[]> AddUsersAsync(
        IEnumerable<AddUserParams> users,
        CancellationToken cancellationToken = default
    )
    {
        var rows = await connection.AddUser(users, cancellationToken: cancellationToken).ConfigureAwait(false);
        return Array.ConvertAll(rows, row => row.Id);
    }

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

    // The query's -- generator: method-type=both gives it a synchronous method, GetUserName, which runs on this thread
    // and returns the row, or null, and takes no CancellationToken, for code that does not await.
    public string? GetUserName(int id) => connection.GetUserName(new GetUserNameParams(id))?.Name;

    // Its asynchronous method, which method-type=both names GetUserNameAsync whatever async-method-suffix says.
    public async Task<string?> GetUserNameAsync(int id, CancellationToken cancellationToken = default) =>
        (
            await connection
                .GetUserNameAsync(new GetUserNameParams(id), cancellationToken: cancellationToken)
                .ConfigureAwait(false)
        )?.Name;

    // The query's -- generator: collection-method method-type=sync gives RenameUser a synchronous collection method,
    // which renames every user in one transaction of its own, on this thread, sending up to 1,000 in each round trip,
    // and returns the count of rows updated.  It takes no CancellationToken, and a user the database refuses throws
    // CollectionItemException, whose ItemIndex is its position, after the transaction has been rolled back.
    public int RenameUsers(IEnumerable<RenameUserParams> renames) => connection.RenameUser(renames);
}
