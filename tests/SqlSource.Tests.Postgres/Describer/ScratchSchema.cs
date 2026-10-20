namespace SqlSource.Tests.Postgres.Describer;

// The objects that more than one test class of the UsesPostgres collection adds to its one server beyond the
// fixture, made by SQL that can be run again, and left: the container is thrown away, and the fixtures are recorded
// in one of their own.  Each object has its text here alone, so a class that runs it first makes the same object as
// any other would.
internal static class ScratchSchema
{
    /// <summary>
    /// A table partitioned by <c>kind</c> into <c>scratch_parts_1</c> and <c>scratch_parts_2</c>, of which
    /// <c>note</c> is the one column that may be <c>NULL</c>.  It is empty: a test that needs rows inserts them in a
    /// transaction it rolls back.
    /// </summary>
    public const string Partitioned = """
        CREATE TABLE IF NOT EXISTS scratch_parts (id integer NOT NULL, kind integer NOT NULL, note text)
            PARTITION BY LIST (kind);
        CREATE TABLE IF NOT EXISTS scratch_parts_1 PARTITION OF scratch_parts FOR VALUES IN (1);
        CREATE TABLE IF NOT EXISTS scratch_parts_2 PARTITION OF scratch_parts FOR VALUES IN (2);
        """;

    /// <summary>
    /// Aggregates <c>shadow.count(*)</c> and <c>shadow.count(integer)</c> that always return <c>NULL</c>.  On a path
    /// that names <c>shadow</c> before <c>pg_catalog</c> the plan prints the first as <c>count(*)</c>; on any path that
    /// names <c>shadow</c>, the second beats <c>pg_catalog.count("any")</c> for an integer by its exact type and prints
    /// as <c>count(id)</c>.
    /// </summary>
    public const string ShadowCount = """
        CREATE SCHEMA IF NOT EXISTS shadow;
        CREATE OR REPLACE FUNCTION shadow.nulls(int8) RETURNS int8 LANGUAGE sql AS 'SELECT NULL::int8';
        CREATE OR REPLACE AGGREGATE shadow.count(*) (sfunc = shadow.nulls, stype = int8);
        CREATE OR REPLACE FUNCTION shadow.nulls(int8, integer) RETURNS int8 LANGUAGE sql AS 'SELECT NULL::int8';
        CREATE OR REPLACE AGGREGATE shadow.count(integer) (sfunc = shadow.nulls, stype = int8);
        """;
}
