using SqlSource;

namespace Consumer;

// Every .sql file of the Queries folder, as members of the type itself.
[SqlSourceGenerate(Path = "Queries", Mode = SqlQueriesMode.Direct)]
internal static partial class Queries;
