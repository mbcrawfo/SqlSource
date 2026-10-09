namespace SqlSource.Settings;

/// <summary>
/// The settings in effect for one query, for one type that claims its file.
/// </summary>
/// <param name="Parameters">The list of generator parameters.</param>
/// <param name="Output">What is generated for the query.  It has no effect yet.</param>
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

    /// <summary>Whether comments and blank lines stay in the query's SQL.</summary>
    public bool KeepComments => (Parameters & GeneratorParameters.KeepComments) != 0;

    /// <summary>Whether the query's method checks its arguments.</summary>
    public bool ValidateTokens => (Parameters & GeneratorParameters.NoTokenValidation) == 0;

    /// <summary>
    /// Resolves each setting from the most specific level that has it: the markers of the query and its file, then
    /// the attribute, then the metadata of the file's item, then the project's property, then the default.  Only
    /// <see cref="Parameters" /> has an effect yet.
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
}
