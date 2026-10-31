namespace SqlSource.DatabaseTests.Postgres.EndToEnd.Json.Types;

// The element of the List<Tag> that the column types of Queries/Json.sql give tags, a jsonb: the serializer reads and
// sends the list whole, as one JSON array.
internal sealed record Tag(string Name);
