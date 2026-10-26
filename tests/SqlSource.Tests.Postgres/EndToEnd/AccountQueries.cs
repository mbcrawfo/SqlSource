namespace SqlSource.Tests.Postgres.EndToEnd;

// The project's settings by property: the output codegen, whose methods extend the connection in
// AccountQueriesExtensions.
[SqlSourceGenerate(Path = "Queries/Accounts.sql")]
internal static partial class AccountQueries;

// The same queries under no-array-pool, which read their rows into a List, for the allocation tests: a second claim in
// the namespace of AccountQueries, whose models it shares, with its methods on itself, so that no extension method is
// given twice.
[SqlSourceGenerate(Path = "Queries/Accounts.sql", MethodLocation = MethodLocation.Public, Parameters = "no-array-pool")]
internal static partial class UnpooledAccountQueries;
