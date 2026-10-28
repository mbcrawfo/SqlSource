namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// The project's settings by its settings file: the output codegen, whose methods extend the connection in
// AccountQueriesExtensions.
[SqlSourceGenerate(Path = "Queries/Accounts.sql")]
internal static partial class AccountQueries;

// The same queries under no-array-pool, which read their rows into a List: a second claim in the namespace of
// AccountQueries, whose models it shares, with its methods on itself, so that no extension method is given twice.  The
// end-to-end project keeps it to show that a second claim of one file, under other generator parameters, builds beside
// the first; tests/DatabaseTests/QueryMethodAllocation claims the file the same way for its allocation tests.
[SqlSourceGenerate(Path = "Queries/Accounts.sql", MethodLocation = MethodLocation.Public, Parameters = "no-array-pool")]
internal static partial class UnpooledAccountQueries;
