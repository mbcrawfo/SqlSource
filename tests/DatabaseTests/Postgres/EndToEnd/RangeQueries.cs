namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// The queries of a range of a user's own and its multirange, read and sent through the manual's type override.
[SqlSourceGenerate(Source = "Queries/Ranges.sql")]
internal static partial class RangeQueries;
