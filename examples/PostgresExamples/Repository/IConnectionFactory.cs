using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using SqlSource;

namespace PostgresExamples.Repository;

// The factory that opens the application's connections, marked once here, so that a repository that holds one, such
// as OrderRepository, needs no member of its own to give its methods their connection.  Each repository method calls
// OpenAsync, runs its query and disposes the connection.  ConnectionFactory implements it over the application's data
// source.  See site/reference/repository-connections.md, "A factory that a repository holds".
public interface IConnectionFactory
{
    [SqlSourceConnection]
    ValueTask<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken);
}
