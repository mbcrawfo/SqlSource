using SqlSource;

namespace PostgresExamples.Repository;

// A repository that gets its connection from a factory it holds: each method of OrderRepository.sql, such as
// ListOrdersOfUser(new ListOrdersOfUserParams(1), cancellationToken), calls connections.OpenAsync, the member that
// IConnectionFactory marks [SqlSourceConnection], runs the query and disposes the connection.  The parameter needs no
// mark.  See site/reference/repository-connections.md, "A factory that a repository holds".
[SqlSourceGenerate]
public sealed partial class OrderRepository(IConnectionFactory connections);
