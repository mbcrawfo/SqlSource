#pragma warning disable IDE0130 // The end-to-end project's namespace, which the mirrored queries name.
namespace SqlSource.DatabaseTests.SqlServer.EndToEnd.Json.Types;

#pragma warning restore IDE0130

// This project's copy of tests/DatabaseTests/SqlServer/EndToEnd/Json/Types/OrderMetadata.cs, in its namespace, since a
// test project compiles no file of another: the mirrored queries name it.  A change to the original changes this copy.
//
// A class of the project that Queries/Json.sql gives settings_meta, header_meta and marker_meta, each an nvarchar, by
// the opt-in, and Queries/JsonColumns.sql gives meta, a json: a method reads and sends it through System.Text.Json's
// serializer with its default options, so its properties are stored by their own names, OrderId and Note.  It has no
// converter, so on plain_meta, an nvarchar without the opt-in, the method asks SqlClient for it, which refuses.
#pragma warning disable CA1812 // Made by the serializer, until a scenario sends one.
internal sealed record OrderMetadata(int OrderId, string? Note);
