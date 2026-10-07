namespace SqlSource.Parsing;

/// <summary>
/// What a dialect setting names: a dialect, and the options of that dialect that are on.
/// </summary>
/// <remarks>
/// The default value is <see cref="SqlDialect.Ansi" /> with no options.  Compared by value, so it can travel between
/// the steps of the generator's pipeline.
/// </remarks>
internal readonly record struct SqlDialectChoice(SqlDialect Dialect, SqlDialectOptions Options)
{
    /// <summary>A dialect with no options.</summary>
    public static implicit operator SqlDialectChoice(SqlDialect dialect) => new(dialect, SqlDialectOptions.None);
}
