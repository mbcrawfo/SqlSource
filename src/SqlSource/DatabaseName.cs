using System;

namespace SqlSource;

/// <summary>
/// The rule for comparing the names of databases, which the generator and the tool share.
/// </summary>
internal static class DatabaseName
{
    /// <summary>
    /// The one comparer of database names: two names that differ only in case read one connection variable, so they
    /// are one database.
    /// </summary>
    public static StringComparer Comparer => StringComparer.OrdinalIgnoreCase;
}
