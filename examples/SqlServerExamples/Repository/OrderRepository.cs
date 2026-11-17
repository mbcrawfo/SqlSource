using SqlSource;

namespace SqlServerExamples.Repository;

// A repository with no member that gives a connection: each method of OrderRepository.sql, such as
// ListOrdersOfUser(new ListOrdersOfUserParams(1), cancellationToken), makes a SqlConnection of
// Connections.ConnectionString, the static property that the project marks [SqlSourceConnection], opens it, runs the
// query and disposes it.  See site/reference/repository-connections.md, "A static member of the project".
[SqlSourceGenerate]
public sealed partial class OrderRepository;
