using SqlSource;

namespace Consumer;

// Every .sql file of the Queries folder, as members of the type itself.
[SqlSourceGenerate(Path = "Queries", SqlLocation = SqlLocation.Direct)]
internal static partial class Queries;

// One of those files again, for a type whose attribute asks for comments.
[SqlSourceGenerate(Path = "Queries/Users.sql", SqlLocation = SqlLocation.Direct, Parameters = "keep-comments")]
internal static partial class KeptQueries;
