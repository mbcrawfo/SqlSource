using SqlSource;

namespace SqlServerExamples.Repository;

// The default output, repository, and the default file, UserRepository.sql beside this one, which need nothing on the
// attribute.  Each query is a public method of this class, such as
// GetUserAsync(new GetUserParams(1), cancellationToken), which makes a SqlConnection of the connection string, opens
// it, runs the query and disposes the connection, so the caller passes no connection.  The synchronous GetUser, of
// method-type=both, does the same with the driver's synchronous calls.  The connection string is found by its name,
// connectionString, and type, string, since SqlClient has no data source.  The class also gets the interface
// IUserRepository, of those methods, for code that takes a repository, as UserGreeter does.  See
// site/settings.md#setting-output.
[SqlSourceGenerate]
public sealed partial class UserRepository(string connectionString);
