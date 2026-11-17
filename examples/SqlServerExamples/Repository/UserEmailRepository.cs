using SqlSource;

namespace SqlServerExamples.Repository;

// A repository whose one query, in UserEmailRepository.sql beside this one, has -- generator: method-type=both.  Its
// synchronous method, GetUserEmail, makes a SqlConnection of the connection string and opens it with Open, and its
// asynchronous one, GetUserEmailAsync, with OpenAsync.  See
// site/reference/repositories.md#synchronous-repository-methods.
[SqlSourceGenerate]
public sealed partial class UserEmailRepository(string connectionString);
