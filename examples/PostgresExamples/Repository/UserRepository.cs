using Npgsql;
using SqlSource;

namespace PostgresExamples.Repository;

// The default output, repository, and the default file, UserRepository.sql beside this one, which need nothing on the
// attribute.  Each query is a public method of this class, such as GetUser(new GetUserParams(1), cancellationToken),
// which opens a connection of the data source, runs the query and disposes the connection, so the caller passes no
// connection.  The data source is found by its type, NpgsqlDataSource, which derives from DbDataSource.  The class also
// gets the interface IUserRepository, of those methods, for code that takes a repository, as UserGreeter does.  See
// docs/manual/settings.md#setting-output.
[SqlSourceGenerate]
public sealed partial class UserRepository(NpgsqlDataSource dataSource);
