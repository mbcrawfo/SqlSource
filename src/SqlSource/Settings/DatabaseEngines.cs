using System;
using System.Collections.Immutable;

namespace SqlSource.Settings;

/// <summary>
/// The members of <see cref="DatabaseEngine" />.  Every loop over the engines reads <see cref="All" />, and
/// <see cref="PerEngine{T}" /> relies on the members running from 0 with no gaps, which <c>DatabaseEnginesTests</c>
/// checks.
/// </summary>
internal static class DatabaseEngines
{
    /// <summary>Every engine, in the order the enum declares them.</summary>
    public static ImmutableArray<DatabaseEngine> All { get; } =
    [.. (DatabaseEngine[])Enum.GetValues(typeof(DatabaseEngine))];
}
