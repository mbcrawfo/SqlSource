namespace SqlSource.Parsing;

/// <summary>
/// What a dialect setting names: a dialect, the options of that dialect that are on, and the version of its server,
/// zero when none is given, as a versioned comment writes it: <c>80036</c> for 8.0.36.
/// </summary>
/// <remarks>
/// The default value is <see cref="SqlDialect.Ansi" /> with no options.  Compared by value, so it can travel between
/// the steps of the generator's pipeline.
/// </remarks>
internal readonly record struct SqlDialectChoice(SqlDialect Dialect, SqlDialectOptions Options, int ServerVersion = 0)
{
    /// <summary>A dialect with no options.</summary>
    public static implicit operator SqlDialectChoice(SqlDialect dialect) => new(dialect, SqlDialectOptions.None);
}
