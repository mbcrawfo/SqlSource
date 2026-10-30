using System;
using SqlSource.Settings;

namespace SqlSource.Generation.Converters;

/// <summary>
/// The engines and drivers that a library's converter serves, as flags.  PostgreSQL is two, since Npgsql 10 reads and
/// writes some types that Npgsql 9 and before do not.
/// </summary>
[Flags]
internal enum ConverterTargets
{
    /// <summary>No engine or driver.</summary>
    None = 0,

    /// <summary>SQL Server.</summary>
    SqlServer = 1,

    /// <summary>PostgreSQL through a version of Npgsql before 10.</summary>
    NpgsqlBefore10 = 2,

    /// <summary>PostgreSQL through Npgsql 10 or later.</summary>
    Npgsql10 = 4,

    /// <summary>PostgreSQL through any version of Npgsql.</summary>
    PostgreSql = NpgsqlBefore10 | Npgsql10,

    /// <summary>Every engine and driver.</summary>
    All = SqlServer | PostgreSql,
}

/// <summary>The target of a query's engine and driver.</summary>
internal static class ConverterTargetsExtensions
{
    /// <summary>
    /// The one target that a query of <paramref name="engine" /> is on: <see cref="ConverterTargets.SqlServer" />, or
    /// for PostgreSQL <see cref="ConverterTargets.Npgsql10" /> when <paramref name="npgsqlAtLeast10" />, else
    /// <see cref="ConverterTargets.NpgsqlBefore10" />.
    /// </summary>
    public static ConverterTargets Of(DatabaseEngine engine, bool npgsqlAtLeast10)
    {
        if (engine == DatabaseEngine.SqlServer)
        {
            return ConverterTargets.SqlServer;
        }

        return npgsqlAtLeast10 ? ConverterTargets.Npgsql10 : ConverterTargets.NpgsqlBefore10;
    }
}
