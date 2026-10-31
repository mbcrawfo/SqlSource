namespace SqlSource.DatabaseTests.SqlServer.EndToEnd.Json.Types;

// A class of the project that Queries/Json.sql gives settings_meta, header_meta and marker_meta, each an nvarchar, by
// the opt-in, and Queries/JsonColumns.sql gives meta, a json: a method reads and sends it through System.Text.Json's
// serializer with its default options, so its properties are stored by their own names, OrderId and Note.  It has no
// converter, so on plain_meta, an nvarchar without the opt-in, the method asks SqlClient for it, which refuses.
internal sealed record OrderMetadata(int OrderId, string? Note);
