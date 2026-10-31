namespace SqlSource.DatabaseTests.Postgres.EndToEnd.Json.Types;

// A class of the project that the column types of Queries/Json.sql give meta, a jsonb, metas, a jsonb[], by element,
// and text_meta, a text, by the opt-in: a method reads and sends it through System.Text.Json's serializer with its
// default options, so its properties are stored by their own names, OrderId and Note.
internal sealed record OrderMetadata(int OrderId, string? Note);
