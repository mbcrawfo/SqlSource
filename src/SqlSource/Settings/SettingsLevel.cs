using SqlSource.Settings.ColumnTypes;
using SqlSource.Settings.Libraries;
using SqlSource.Settings.TypeOverrides;

namespace SqlSource.Settings;

/// <summary>
/// What one source says about the settings: a settings file, or the chain of a folder's settings files, the attribute
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
    /// The name of the database a query belongs to.  The generator carries it, from a marker or a settings file, and
    /// never uses it; the tool does.
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

    /// <summary>
    /// The entries of the type override whose keys PostgreSQL reads, keyed by its rules, sorted by key as
    /// <see cref="TypeOverrideMap" /> keeps them, entries of <c>default</c> included.  Merged per key, not taken whole.
    /// </summary>
    public EquatableArray<TypeOverride>? TypeOverrides { get; init; }

    /// <summary>
    /// The entries of the type override whose keys SQL Server reads, keyed by its rules, kept as
    /// <see cref="TypeOverrides" /> is.  An object of a settings file fills both, and a marker the one of its file's
    /// engine.
    /// </summary>
    public EquatableArray<TypeOverride>? SqlServerTypeOverrides { get; init; }

    /// <summary>
    /// The column types: the C# types of columns by their source, sorted by key as <see cref="ColumnTypeMap" /> keeps
    /// them, entries of <c>default</c> included.  One list for both engines, whose keys are read alike.  Merged per
    /// key, not taken whole.
    /// </summary>
    public EquatableArray<ColumnType>? ColumnTypes { get; init; }

    /// <summary>
    /// What the level says about each library whose types SqlSource can use, sorted by library as
    /// <see cref="LibrarySettings" /> keeps them, entries of <c>default</c> included.  Merged per library, not taken
    /// whole.
    /// </summary>
    public EquatableArray<LibrarySetting>? LibraryTypes { get; init; }

    /// <summary>
    /// The name of the class that holds a type's members in <see cref="MemberPlacement.Nested" /> placement.  The
    /// attribute and the chain of the folder of the type's C# file give it; no <c>.sql</c> file's level does, since a
    /// type's queries can come from several files.
    /// </summary>
    public string? NestedClass { get; init; }

    /// <summary>Whether the level gives a list that has <c>keep-comments</c>.</summary>
    public bool KeepsComments => Parameters is { } list && (list & GeneratorParameters.KeepComments) != 0;

    /// <summary>
    /// This level over <paramref name="other" />: each member from this level when it has one, and from
    /// <paramref name="other" /> when it has not.  The type override of each engine, the column types and the library
    /// types are merged per key, this level's entry winning.
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
            TypeOverrides = TypeOverrideMap.Over(TypeOverrides, other.TypeOverrides),
            SqlServerTypeOverrides = TypeOverrideMap.Over(SqlServerTypeOverrides, other.SqlServerTypeOverrides),
            ColumnTypes = ColumnTypeMap.Over(ColumnTypes, other.ColumnTypes),
            LibraryTypes = LibrarySettings.Over(LibraryTypes, other.LibraryTypes),
            NestedClass = NestedClass ?? other.NestedClass,
        };
    }
}
