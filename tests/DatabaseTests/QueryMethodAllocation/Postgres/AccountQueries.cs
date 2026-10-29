namespace SqlSource.DatabaseTests.QueryMethodAllocation.Postgres;

// The claim of the PostgreSQL end-to-end project's AccountQueries, repeated so that the methods measured here are the
// ones that project runs: the output codegen, whose methods extend the connection in AccountQueriesExtensions.
[SqlSourceGenerate(Path = "../../Postgres/EndToEnd/Queries/Accounts.sql")]
internal static partial class AccountQueries;

// The claim of that project's UnpooledAccountQueries, repeated for the same reason: the same queries under
// no-array-pool, which read their rows into a List, a second claim in the namespace of AccountQueries, whose models it
// shares, with its methods on itself, so that no extension method is given twice.
[SqlSourceGenerate(
    Path = "../../Postgres/EndToEnd/Queries/Accounts.sql",
    MethodLocation = MethodLocation.Public,
    Parameters = "no-array-pool"
)]
internal static partial class UnpooledAccountQueries;
