namespace SqlSource.Tests.Postgres.EndToEnd;

// A folder of the database billing, with the model settings of the project's metadata.
[SqlSourceGenerate(Path = "Queries/Billing", SqlLocation = SqlLocation.Direct)]
internal static partial class InvoiceQueries;
