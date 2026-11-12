using System.Threading;
using System.Threading.Tasks;

namespace PostgresExamples.Repository;

// Code that uses a repository through its generated interface, IUserRepository, so that a test of it can pass a fake
// in place of UserRepository and needs no database.
public sealed class UserGreeter(IUserRepository users)
{
    public async Task<string> GreetAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await users.GetUserAsync(new GetUserParams(userId), cancellationToken).ConfigureAwait(false);
        return GreetingOf(user);
    }

    // The query's -- generator: method-type=both gives it a synchronous method, GetUser, for code that does not await,
    // which runs on this thread and takes no cancellation token.
    public string Greet(int userId) => GreetingOf(users.GetUser(new GetUserParams(userId)));

    private static string GreetingOf(GetUserDto? user) => user is null ? "Hello, stranger." : $"Hello, {user.Name}.";
}
