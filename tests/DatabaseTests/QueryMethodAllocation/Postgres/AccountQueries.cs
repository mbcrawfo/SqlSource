namespace SqlSource.DatabaseTests.QueryMethodAllocation.Postgres;

// The claim of the PostgreSQL end-to-end project's AccountQueries, repeated so that the methods measured here are the
// ones that project runs: the output codegen, whose methods extend the connection in AccountQueriesExtensions.  The
// Unpooled copies of its queries in the file are the ones measured under array-pool=off, by the copy's own marker.
[SqlSourceGenerate(Source = "../../Postgres/EndToEnd/Queries/Accounts.sql")]
internal static partial class AccountQueries;
