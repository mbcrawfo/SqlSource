using System;

namespace SqlSource;

/// <summary>The schema each engine keeps its built-in types in, named once.</summary>
internal static class SystemSchemas
{
    /// <summary>PostgreSQL's catalog schema.</summary>
    public const string Postgres = "pg_catalog";

    /// <summary>SQL Server's schema of built-in types.</summary>
    public const string SqlServer = "sys";

    /// <summary>
    /// Whether <paramref name="schema" />, as the server reported it, is SQL Server's own.  The comparison is
    /// ordinal: the server compares a schema by the database's collation and always reports its own as
    /// <c>sys</c>, and in a case-sensitive database <c>SYS</c> is a schema of a user.  A key that a user writes is
    /// another rule, which <c>TypeKeys</c> keeps.
    /// </summary>
    public static bool IsSqlServer(string? schema) => string.Equals(schema, SqlServer, StringComparison.Ordinal);
}
