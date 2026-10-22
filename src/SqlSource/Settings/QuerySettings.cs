using SqlSource.Settings.TypeOverrides;

namespace SqlSource.Settings;

/// <summary>
/// The settings in effect for one query, for one type that claims its file.
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
internal readonly record struct QuerySettings(
    GeneratorParameters Parameters,
    OutputKind Output,
    string InputModelSuffix,
    string OutputModelSuffix,
    string? ModelNamespace,
    ModelKind InputModelType,
    ModelKind OutputModelType,
    CollectionKind CollectionType
)
{
    /// <summary>What ends the name of the type of a query's parameters, unless a level says otherwise.</summary>
    public const string DefaultInputModelSuffix = "Params";

    /// <summary>What ends the name of the type of a row, unless a level says otherwise.</summary>
    public const string DefaultOutputModelSuffix = "Dto";

    /// <summary>
    /// The name of the class that holds a type's members in <see cref="MemberPlacement.Nested" /> placement, unless a
    /// level says otherwise.
    /// </summary>
    public const string DefaultNestedClass = "Sql";

    /// <summary>Whether comments and blank lines stay in the query's SQL.</summary>
    public bool KeepComments => (Parameters & GeneratorParameters.KeepComments) != 0;

    /// <summary>Whether the query's method checks its arguments.</summary>
    public bool ValidateTokens => (Parameters & GeneratorParameters.NoTokenValidation) == 0;

    /// <summary>
    /// Resolves each setting from the most specific level that has it: the markers of the query and its file, then
    /// the attribute, then the metadata of the file's item, then the project's property, then the default.
    /// <see cref="CollectionType" /> has no effect yet.  The type override is not among them:
    /// <see cref="ResolveTypeOverrides" /> gives it.
    /// </summary>
    public static QuerySettings Resolve(
        SettingsLevel markers,
        SettingsLevel attribute,
        SettingsLevel metadata,
        SettingsLevel property
    ) =>
        new(
            markers.Parameters
                ?? attribute.Parameters
                ?? metadata.Parameters
                ?? property.Parameters
                ?? GeneratorParameters.None,
            markers.Output ?? attribute.Output ?? metadata.Output ?? property.Output ?? OutputKind.CodeGen,
            markers.InputModelSuffix
                ?? attribute.InputModelSuffix
                ?? metadata.InputModelSuffix
                ?? property.InputModelSuffix
                ?? DefaultInputModelSuffix,
            markers.OutputModelSuffix
                ?? attribute.OutputModelSuffix
                ?? metadata.OutputModelSuffix
                ?? property.OutputModelSuffix
                ?? DefaultOutputModelSuffix,
            markers.ModelNamespace ?? attribute.ModelNamespace ?? metadata.ModelNamespace ?? property.ModelNamespace,
            markers.InputModelType
                ?? attribute.InputModelType
                ?? metadata.InputModelType
                ?? property.InputModelType
                ?? ModelKind.SealedRecord,
            markers.OutputModelType
                ?? attribute.OutputModelType
                ?? metadata.OutputModelType
                ?? property.OutputModelType
                ?? ModelKind.SealedRecord,
            markers.CollectionType
                ?? attribute.CollectionType
                ?? metadata.CollectionType
                ?? property.CollectionType
                ?? CollectionKind.Array
        );

    /// <summary>
    /// The type override of a query on <paramref name="engine" />: the precedence of the type override, which has no
    /// attribute level and merges per key, the markers over the metadata over the property, each level's entries of
    /// that engine.  Sorted by key, and empty when no level sets one.  It keeps its entries of <c>default</c>, which
    /// <see cref="TypeOverrideMap.Find" /> weighs by their place against the entries of other keys that match a type.
    /// It is kept apart from <see cref="Resolve" />, which runs for each query and each type that claims its file, so
    /// that a caller that needs it computes it once for each distinct level of markers.
    /// </summary>
    public static EquatableArray<TypeOverride> ResolveTypeOverrides(
        SettingsLevel markers,
        SettingsLevel metadata,
        SettingsLevel property,
        DatabaseEngine engine
    ) =>
        engine == DatabaseEngine.SqlServer
            ? Merge(markers.SqlServerTypeOverrides, metadata.SqlServerTypeOverrides, property.SqlServerTypeOverrides)
            : Merge(markers.TypeOverrides, metadata.TypeOverrides, property.TypeOverrides);

    /// <summary>
    /// The database of a query: the markers', then the metadata's, then the property's, else the name of the dialect,
    /// which the caller gives since <c>Settings/</c> does not use <c>Parsing/</c>.  The attribute has no level for
    /// it, so it is kept apart from <see cref="Resolve" />, as <see cref="ResolveTypeOverrides" /> is.
    /// </summary>
    public static string ResolveDatabase(
        SettingsLevel markers,
        SettingsLevel metadata,
        SettingsLevel property,
        string dialectName
    ) => markers.Database ?? metadata.Database ?? property.Database ?? dialectName;

    /// <summary>
    /// The name of a type's nested class: the attribute's, then the property's, else the default.  It belongs to the
    /// type and not to a query, so it has no marker or metadata level and is kept apart from <see cref="Resolve" />.
    /// </summary>
    public static string ResolveNestedClass(SettingsLevel attribute, SettingsLevel property) =>
        attribute.NestedClass ?? property.NestedClass ?? DefaultNestedClass;

    private static EquatableArray<TypeOverride> Merge(
        EquatableArray<TypeOverride>? markers,
        EquatableArray<TypeOverride>? metadata,
        EquatableArray<TypeOverride>? property
    ) => TypeOverrideMap.Over(markers, TypeOverrideMap.Over(metadata, property)) ?? EquatableArray<TypeOverride>.Empty;
}
