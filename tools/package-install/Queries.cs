using SqlSource;

namespace Consumer;

// Every .sql file of the Queries folder, as members of the type itself.
[SqlSourceGenerate(Path = "Queries", SqlLocation = SqlLocation.Direct)]
internal static partial class Queries;
