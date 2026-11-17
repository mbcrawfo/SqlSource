using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace PostgresExamples.Repository;

// The application's IConnectionFactory, which opens each connection of its data source, so that the connections know
// the data source's plugins and mappings.  Dependency injection gives it to every repository that holds an
// IConnectionFactory, such as OrderRepository.  It needs no mark: the interface's OpenAsync carries it.
public sealed class ConnectionFactory(NpgsqlDataSource dataSource) : IConnectionFactory
{
    public ValueTask<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken) =>
        dataSource.OpenConnectionAsync(cancellationToken);
}
