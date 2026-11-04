using SqlSource.Settings.ColumnTypes;
using SqlSource.Settings.Libraries;
using SqlSource.Settings.TypeOverrides;

namespace SqlSource.Settings;

/// <summary>
/// The settings in effect for one query, from its markers and its file's level: the same for every type that claims
/// its file.
/// </summary>
/// <param name="Parameters">The list of generator parameters.</param>
/// <param name="Output">
/// What is generated for the query: whether it needs an entry in its file's sidecar, and whether it gets models.
/// </param>
/// <param name="InputModelSuffix">What ends the name of the type of the query's parameters.</param>
/// <param name="OutputModelSuffix">What ends the name of the type of a row of the query's result.</param>
/// <param name="ModelNamespace">The namespace of the query's models, or null for the namespace of the type.</param>
/// <param name="InputModelType">The shape of the type of the query's parameters.</param>
/// <param name="OutputModelType">The shape of the type of a row.</param>
/// <param name="CollectionType">The type a method returns many rows in.</param>
/// <param name="WrittenMemberLocation">
/// The member location a marker or a settings file wrote, with where, or null when none did.
/// <see cref="MemberLocation" /> is the one in effect.
/// </param>
/// <param name="WrittenVisibility">
/// The visibility a marker or a settings file wrote, with where, or null when none did.
/// </param>
/// <param name="NestedClass">The name of the class that holds the query's members in a nested class.</param>
internal readonly record struct QuerySettings(
    GeneratorParameters Parameters,
    OutputKind Output,
    string InputModelSuffix,
    string OutputModelSuffix,
    string? ModelNamespace,
    ModelKind InputModelType,
    ModelKind OutputModelType,
    CollectionKind CollectionType,
    Placed<MemberLocation>? WrittenMemberLocation,
    Placed<Visibility>? WrittenVisibility,
    string NestedClass
)
{
    /// <summary>What ends the name of the type of a query's parameters, unless a level says otherwise.</summary>
    public const string DefaultInputModelSuffix = "Params";

    /// <summary>What ends the name of the type of a row, unless a level says otherwise.</summary>
    public const string DefaultOutputModelSuffix = "Dto";

    /// <summary>
    /// The name of the class that holds a query's members in <see cref="MemberLocation.NestedClass" /> location, unless
    /// a level says otherwise.
    /// </summary>
    public const string DefaultNestedClass = "Sql";

    /// <summary>
    /// Where the query's members go: the one written, else the default of its output, a nested class for the output
    /// that has no method and the extension class for the one that has.
    /// </summary>
    public MemberLocation MemberLocation =>
        WrittenMemberLocation?.Value
        ?? (Output == OutputKind.CodeGen ? MemberLocation.ExtensionClass : MemberLocation.NestedClass);

    /// <summary>Whether comments and blank lines stay in the query's SQL.</summary>
    public bool KeepComments => (Parameters & GeneratorParameters.KeepComments) != 0;

    /// <summary>Whether the query's token method and query method check that each token's argument has text.</summary>
    public bool ValidateTokens => (Parameters & GeneratorParameters.NoTokenValidation) == 0;

    /// <summary>Whether the query's method may read its rows into buffers from the shared array pool.</summary>
    public bool UseArrayPool => (Parameters & GeneratorParameters.NoArrayPool) == 0;

    /// <summary>
    /// Resolves each setting from the most specific level that has it: the markers of the query and its file, then
    /// the file's level, then the default.  The file's level is the chain of its folder, so the nearest settings file
    /// that has a setting gives it.  The type override, the column types and the library types are not among them:
    /// <see cref="ResolveTypeOverrides" />, <see cref="ResolveColumnTypes" /> and <see cref="ResolveLibraries" /> give
    /// them.
    /// </summary>
    public static QuerySettings Resolve(SettingsLevel markers, SettingsLevel file) =>
        new(
            markers.Parameters ?? file.Parameters ?? GeneratorParameters.None,
            markers.Output ?? file.Output ?? OutputKind.CodeGen,
            markers.InputModelSuffix ?? file.InputModelSuffix ?? DefaultInputModelSuffix,
            markers.OutputModelSuffix ?? file.OutputModelSuffix ?? DefaultOutputModelSuffix,
            markers.ModelNamespace ?? file.ModelNamespace,
            markers.InputModelType ?? file.InputModelType ?? ModelKind.SealedRecord,
            markers.OutputModelType ?? file.OutputModelType ?? ModelKind.SealedRecord,
            markers.CollectionType ?? file.CollectionType ?? CollectionKind.Array,
            markers.MemberLocation ?? file.MemberLocation,
            markers.Visibility ?? file.Visibility,
            markers.NestedClass ?? file.NestedClass ?? DefaultNestedClass
        );

    /// <summary>
    /// The type override of a query on <paramref name="engine" />: the precedence of the type override, which has no
    /// attribute level and merges per key, the markers over the file's level, each level's entries of that engine.
    /// The file's level is the chain of its folder, whose files merge per key the same way.  Sorted by key, and empty
    /// when no level sets one.  It keeps its entries of <c>default</c>, which <see cref="TypeOverrideMap.Find" />
    /// weighs by their place against the entries of other keys that match a type, a nearer settings file's place above
    /// a farther one's.  It is kept apart from <see cref="Resolve" />, which runs for each query and each type that
    /// claims its file, so that a caller that needs it computes it once for each distinct level of markers.
    /// </summary>
    public static EquatableArray<TypeOverride> ResolveTypeOverrides(
        SettingsLevel markers,
        SettingsLevel file,
        DatabaseEngine engine
    ) =>
        (
            engine switch
            {
                DatabaseEngine.PostgreSql => TypeOverrideMap.Over(markers.TypeOverrides, file.TypeOverrides),
                DatabaseEngine.SqlServer => TypeOverrideMap.Over(
                    markers.SqlServerTypeOverrides,
                    file.SqlServerTypeOverrides
                ),
            }
        ) ?? EquatableArray<TypeOverride>.Empty;

    /// <summary>
    /// The column types of a query: the markers' over the file's level's, merged per key as the type override is in
    /// <see cref="ResolveTypeOverrides" />, and one list for both engines.  The file's level is the chain of its
    /// folder.  The markers' are those of the file's header, by the column's source; a query's own <c>-- column:</c>
    /// markers name its result's columns and are not among them.  Sorted by key, and empty when no level sets one.  It
    /// keeps its entries of <c>default</c>, which <see cref="ColumnTypeMap.Find" /> weighs by their place, and is kept
    /// apart from <see cref="Resolve" /> for the same reason as the type override.
    /// </summary>
    public static EquatableArray<ColumnType> ResolveColumnTypes(SettingsLevel markers, SettingsLevel file) =>
        ColumnTypeMap.Over(markers.ColumnTypes, file.ColumnTypes) ?? EquatableArray<ColumnType>.Empty;

    /// <summary>
    /// The library types of a query: the markers' over the file's level's, merged per library, so a level replaces
    /// only the libraries it names.  The file's level is the chain of its folder.  Sorted by library, and empty when no
    /// level names one.  It keeps its entries of <c>default</c>, which hide the farther levels' so that the engine's
    /// default applies, and is kept apart from <see cref="Resolve" /> for the same reason as the type override.
    /// </summary>
    public static EquatableArray<LibrarySetting> ResolveLibraries(SettingsLevel markers, SettingsLevel file) =>
        LibrarySettings.Over(markers.LibraryTypes, file.LibraryTypes) ?? EquatableArray<LibrarySetting>.Empty;

    /// <summary>
    /// The database of a query: the markers', then the file's level's, else the name of the dialect, which the caller
    /// gives since <c>Settings/</c> does not use <c>Parsing/</c>.  The file's level is the chain of its folder.  It
    /// needs that name, so it is kept apart from <see cref="Resolve" />.
    /// </summary>
    public static string ResolveDatabase(SettingsLevel markers, SettingsLevel file, string dialectName) =>
        markers.Database ?? file.Database ?? dialectName;

    /// <summary>
    /// The name of a type's nested class: the attribute's, then the chain's, else the default.  The chain is that of
    /// the folder of the type's C# file.  The class belongs to the type and not to a query, so it has no marker level,
    /// and no <c>.sql</c> file's chain decides it; it is kept apart from <see cref="Resolve" />.
    /// </summary>
    public static string ResolveNestedClass(SettingsLevel attribute, SettingsLevel chain) =>
        attribute.NestedClass ?? chain.NestedClass ?? DefaultNestedClass;
}
