namespace SqlSource.DatabaseTests.SqlServer.EndToEnd.Json.Types;

// The element of the List<Tag> that the header of Queries/Json.sql gives tags, an nvarchar, by the opt-in: the
// serializer reads and sends the list whole, as one JSON array.
internal sealed record Tag(string Name);
