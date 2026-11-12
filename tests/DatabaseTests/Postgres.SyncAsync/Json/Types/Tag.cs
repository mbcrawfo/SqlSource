#pragma warning disable IDE0130 // The end-to-end project's namespace, which the mirrored queries name.
namespace SqlSource.DatabaseTests.Postgres.EndToEnd.Json.Types;

#pragma warning restore IDE0130

// This project's copy of tests/DatabaseTests/Postgres/EndToEnd/Json/Types/Tag.cs, in its namespace, since a test
// project compiles no file of another: the mirrored queries name it.  A change to the original changes this copy.
//
// The element of the List<Tag> that the column types of Queries/Json.sql give tags, a jsonb: the serializer reads and
// sends the list whole, as one JSON array.
internal sealed record Tag(string Name);
