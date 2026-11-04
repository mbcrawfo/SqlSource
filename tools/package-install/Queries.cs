using SqlSource;

namespace Consumer;

// Every .sql file of the Queries folder, as members of the type itself, which sqlsource.json's memberLocation says.
[SqlSourceGenerate(Source = "Queries")]
internal static partial class Queries;
