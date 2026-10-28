namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// A folder of the database billing, with the model settings of the folder's settings file.
[SqlSourceGenerate(Path = "Queries/Billing", SqlLocation = SqlLocation.Direct)]
internal static partial class InvoiceQueries;
