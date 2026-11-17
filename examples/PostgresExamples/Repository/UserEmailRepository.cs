using Npgsql;
using SqlSource;

namespace PostgresExamples.Repository;

// A repository whose one query, in UserEmailRepository.sql beside this one, has -- generator: method-type=both.  Its
// synchronous method, GetUserEmail, opens its connection of the data source with OpenConnection, and its asynchronous
// one, GetUserEmailAsync, with OpenConnectionAsync.  A synchronous method needs a data source whose connection string
// does not set Multiplexing=true.  See site/reference/repositories.md#synchronous-repository-methods.
[SqlSourceGenerate]
public sealed partial class UserEmailRepository(NpgsqlDataSource dataSource);
