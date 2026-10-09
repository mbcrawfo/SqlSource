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

    /// <summary>What is generated for a query.</summary>
    public OutputKind? Output { get; init; }

    /// <summary>
    /// The name of the database a query belongs to.  The generator carries it from a marker and never reads it; the
    /// tool does.
    /// </summary>
    public string? Database { get; init; }

    /// <summary>What ends the name of the type of a query's parameters.</summary>
    public string? InputModelSuffix { get; init; }

    /// <summary>What ends the name of the type of a row of a query's result.</summary>
    public string? OutputModelSuffix { get; init; }

    /// <summary>The namespace of the query's models.</summary>
    public string? ModelNamespace { get; init; }

    /// <summary>The shape of the type of a query's parameters.</summary>
    public ModelKind? InputModelType { get; init; }

    /// <summary>The shape of the type of a row of a query's result.</summary>
    public ModelKind? OutputModelType { get; init; }

    /// <summary>The type a method returns many rows in.</summary>
    public CollectionKind? CollectionType { get; init; }

    /// <summary>Whether the level gives a list that has <c>keep-comments</c>.</summary>
    public bool KeepsComments => Parameters is { } list && (list & GeneratorParameters.KeepComments) != 0;

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

        return new SettingsLevel
        {
            Parameters = Parameters ?? other.Parameters,
            Output = Output ?? other.Output,
            Database = Database ?? other.Database,
            InputModelSuffix = InputModelSuffix ?? other.InputModelSuffix,
            OutputModelSuffix = OutputModelSuffix ?? other.OutputModelSuffix,
            ModelNamespace = ModelNamespace ?? other.ModelNamespace,
            InputModelType = InputModelType ?? other.InputModelType,
            OutputModelType = OutputModelType ?? other.OutputModelType,
            CollectionType = CollectionType ?? other.CollectionType,
        };
    }
}
