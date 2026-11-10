using System.Threading;
using System.Threading.Tasks;

namespace SqlServerExamples.Repository;

// Code that uses a repository through its generated interface, IUserRepository, so that a test of it can pass a fake
// in place of UserRepository and needs no database.
public sealed class UserGreeter(IUserRepository users)
{
    public async Task<string> GreetAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await users.GetUser(new GetUserParams(userId), cancellationToken).ConfigureAwait(false);
        return user is null ? "Hello, stranger." : $"Hello, {user.Name}.";
    }
}
