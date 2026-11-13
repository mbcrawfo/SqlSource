#pragma warning disable IDE0130 // The end-to-end project's namespace, which the mirrored queries name.
namespace SqlSource.DatabaseTests.SqlServer.EndToEnd.Types;

#pragma warning restore IDE0130

// This project's copy of tests/DatabaseTests/SqlServer/EndToEnd/Types/ExternalId.cs, in its namespace, since a test
// project compiles no file of another: the mirrored queries name it.  A change to the original changes this copy.
//
// The id that the schema's alias type dbo.ExternalId is read as, by the type override of Queries/ExternalRefs.sql: a
// value type, which a method reads from the long that SqlClient gives an alias type over bigint and sends as that long,
// through its converters.
internal readonly record struct ExternalId(long Value)
{
    [SqlSourceConverter]
    public static ExternalId FromDatabase(long value) => new(value);

    [SqlSourceConverter]
    public static long ToDatabase(ExternalId externalId) => externalId.Value;
}
