namespace SqlSource.DatabaseTests.SqlServer.EndToEnd.Types;

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
