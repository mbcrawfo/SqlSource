namespace SqlSource.Tests.Postgres.EndToEnd;

// A folder of the database billing, whose model shape, suffix and type override come from the project's metadata.
[SqlSourceGenerate(Path = "Queries/Billing")]
internal static partial class InvoiceQueries;
