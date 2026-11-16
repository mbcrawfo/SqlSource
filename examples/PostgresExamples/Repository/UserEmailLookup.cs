using System.Threading;
using System.Threading.Tasks;

namespace PostgresExamples.Repository;

// Code that uses a repository's synchronous method through its generated interface, IUserEmailRepository.
public sealed class UserEmailLookup(IUserEmailRepository emails)
{
    // The query's -- generator: method-type=both gives it a synchronous method, GetUserEmail, for code that does not
    // await, which runs on this thread and takes no cancellation token.
    public string? EmailOf(int userId) => emails.GetUserEmail(new GetUserEmailParams(userId))?.Email;

    // Its asynchronous method, which method-type=both names GetUserEmailAsync.
    public async Task<string?> EmailOfAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await emails
            .GetUserEmailAsync(new GetUserEmailParams(userId), cancellationToken)
            .ConfigureAwait(false);
        return user?.Email;
    }
}
