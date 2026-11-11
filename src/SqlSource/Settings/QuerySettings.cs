using SqlSource.Settings.ColumnTypes;
using SqlSource.Settings.Libraries;
using SqlSource.Settings.TypeOverrides;

namespace SqlSource.Settings;

/// <summary>
/// The settings in effect for one query, from its markers and its file's level: the same for every type that claims
/// its file.
/// </summary>
/// <param name="Parameters">The value of each generator parameter.</param>
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
    GeneratorParameterValues Parameters,
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
    /// that has no method, the extension class for a method that extends the connection, and the type for the
    /// repository.
    /// </summary>
    public MemberLocation MemberLocation =>
        WrittenMemberLocation?.Value
        ?? Output switch
        {
            OutputKind.Methods => MemberLocation.ExtensionClass,
            OutputKind.Repository => MemberLocation.Class,
            OutputKind.Sql or OutputKind.Models => MemberLocation.NestedClass,
            _ => throw UnexpectedEnumValueException.Of(Output),
        };

    /// <summary>Whether comments and blank lines stay in the query's SQL.</summary>
    public bool KeepComments => Parameters.IsOn(GeneratorParameters.KeepComments);

    /// <summary>Whether the query's token method and query method check that each token's argument has text.</summary>
    public bool ValidateTokens => Parameters.IsOn(GeneratorParameters.TokenValidation);

    /// <summary>Whether the query's method may read its rows into buffers from the shared array pool.</summary>
    public bool UseArrayPool => Parameters.IsOn(GeneratorParameters.ArrayPool);

    /// <summary>Whether the query's method pads each list query parameter to a fixed size.</summary>
    public bool PadsLists => Parameters.IsOn(GeneratorParameters.ListPadding);

    /// <summary>
    /// Whether a query with parameters whose output is <c>methods</c> or <c>repository</c> also gets a collection
    /// method, which runs it for each item of a collection.
    /// </summary>
    public bool CollectionMethod => Parameters.IsOn(GeneratorParameters.CollectionMethod);

    /// <summary>How a collection method wraps its runs in a transaction.</summary>
    public TransactionMode Transaction => (TransactionMode)Parameters.Get(GeneratorParameters.Transaction);

    /// <summary>
    /// Resolves each setting from the most specific level that has it: the markers of the query and its file, then
    /// the file's level, then the default.  The file's level is the chain of its folder, so the nearest settings file
    /// that has a setting gives it.  The generator parameters merge per parameter instead: each has the value of the
    /// most specific level that names it, else its default.  The type override, the column types, the library
    /// types and the database are not among them: <see cref="ResolveTypeOverrides" />,
    /// <see cref="ResolveColumnTypes" />, <see cref="ResolveLibraries" /> and <see cref="ResolveDatabase" /> give them.
    /// </summary>
    public static QuerySettings Resolve(SettingsLevel markers, SettingsLevel file) =>
        new(
            markers.Parameters.Over(file.Parameters).Resolve(),
            ResolveOutput(markers, file),
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
    /// The type override of a query on <paramref name="engine" />: the precedence of the type override, which merges
    /// per key, the markers over the file's level, each level's entries of that engine.  The file's level is the chain
    /// of its folder, whose files merge per key the same way.  Sorted by key, and empty when no level sets one.  It
    /// keeps its entries of <c>default</c>, which <see cref="TypeOverrideMap.Find" /> weighs by their place against the
    /// entries of other keys that match a type, a nearer settings file's place above
    /// a farther one's.  It is kept apart from <see cref="Resolve" />, which runs for each query and each type that
    /// claims its file, so that a caller that needs it computes it once for each distinct level of markers.
    /// </summary>
    public static EquatableArray<TypeOverride> ResolveTypeOverrides(
        SettingsLevel markers,
        SettingsLevel file,
        DatabaseEngine engine
    ) =>
        TypeOverrideMap.Over(markers.TypeOverrides[engine], file.TypeOverrides[engine])
        ?? EquatableArray<TypeOverride>.Empty;

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
    /// The output of a query: the markers', then the file's level's, else <see cref="OutputKind.Repository" />.  The
    /// file's level is the chain of its folder.  <see cref="Resolve" /> gives it too; a step that needs only the
    /// output asks this, without resolving the rest.
    /// </summary>
    public static OutputKind ResolveOutput(SettingsLevel markers, SettingsLevel file) =>
        markers.Output ?? file.Output ?? OutputKind.Repository;

    /// <summary>
    /// The database of a query: the markers', then the file's level's, else the name of the dialect, which the caller
    /// gives since <c>Settings/</c> does not use <c>Parsing/</c>.  The file's level is the chain of its folder.  It
    /// needs that name, so it is kept apart from <see cref="Resolve" />.
    /// </summary>
    public static string ResolveDatabase(SettingsLevel markers, SettingsLevel file, string dialectName) =>
        markers.Database ?? file.Database ?? dialectName;
}
