namespace SqlSource.Tests.SqlServer.EndToEnd;

// A folder of the database billing, with the SQL on the type, and the model settings of the project's metadata.
[SqlSourceGenerate(Path = "Queries/Billing", SqlLocation = SqlLocation.Direct)]
internal static partial class InvoiceQueries;
