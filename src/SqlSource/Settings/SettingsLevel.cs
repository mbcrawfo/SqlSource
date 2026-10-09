namespace SqlSource.Settings;

/// <summary>
/// What one source says about the settings: the project's properties, the metadata of a file's item, the attribute
/// of a type, or the markers of a query over those of its file's preamble.
/// </summary>
/// <remarks>
/// Every member is nullable, and null means the source does not say.  A source sets the members it has: not every
/// setting has every source.  <see cref="QuerySettings.Resolve" /> is the one rule that puts the levels together.
/// </remarks>
internal sealed record SettingsLevel
{
    /// <summary>
    /// A level that says nothing.  Shared, so that a query or a file without settings allocates none.
    /// </summary>
    public static SettingsLevel None { get; } = new();

    /// <summary>The list of generator parameters, whole, or null when the level gives no list.</summary>
    public GeneratorParameters? Parameters { get; init; }

    /// <summary>
    /// This level over <paramref name="other" />: each member from this level when it has one, and from
    /// <paramref name="other" /> when it has not.
    /// </summary>
    public SettingsLevel Over(SettingsLevel other)
    {
        if (ReferenceEquals(other, None))
        {
            return this;
        }

        if (ReferenceEquals(this, None))
        {
            return other;
        }

        return new SettingsLevel { Parameters = Parameters ?? other.Parameters };
    }
}
