#pragma warning disable IDE0130 // The end-to-end project's namespace, which the mirrored queries name.
namespace SqlSource.DatabaseTests.Postgres.EndToEnd.Json.Types;

#pragma warning restore IDE0130

// This project's copy of tests/DatabaseTests/Postgres/EndToEnd/Json/Types/OrderMetadata.cs, in its namespace, since a
// test project compiles no file of another: the mirrored queries name it.  A change to the original changes this copy.
//
// A class of the project that the column types of Queries/Json.sql give meta, a jsonb, metas, a jsonb[], by element,
// and text_meta, a text, by the opt-in: a method reads and sends it through System.Text.Json's serializer with its
// default options, so its properties are stored by their own names, OrderId and Note.
#pragma warning disable CA1812 // Made by the serializer, until a scenario sends one.
internal sealed record OrderMetadata(int OrderId, string? Note);
