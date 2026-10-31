namespace SqlSource.DatabaseTests.Postgres.Npgsql8.Types;

// The element of the List<Tag> that the column types of the linked Json.sql give tags, a jsonb: the serializer reads
// and sends the list whole, as one JSON array.
internal sealed record Tag(string Name);
