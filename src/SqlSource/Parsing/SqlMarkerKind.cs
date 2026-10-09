namespace SqlSource.Parsing;

/// <summary>
/// The marker comments the parser gives meaning to.
/// </summary>
internal enum SqlMarkerKind
{
    /// <summary><c>-- name:</c> starts a block.</summary>
    Name,

    /// <summary><c>-- summary:</c> documents a block.</summary>
    Summary,

    /// <summary><c>-- generator:</c> carries generator parameters.</summary>
    GeneratorParameters,

    /// <summary><c>-- dialect:</c> names the dialect of the file.</summary>
    Dialect,

    /// <summary><c>-- token:</c> gives a token of its query a default.</summary>
    Token,

    /// <summary><c>-- token-ignore:</c> names a token of its query that stays literal text.</summary>
    TokenIgnore,

    /// <summary>
    /// <c>-- param:</c> declares a parameter of its query: its type, whether it is nullable, or both.
    /// </summary>
    Param,

    /// <summary><c>-- database:</c> names the database of a query, or of every query of its file.</summary>
    Database,

    /// <summary><c>-- output:</c> says what is generated for a query, or for every query of its file.</summary>
    Output,

    /// <summary><c>-- input-model-suffix:</c> ends the names of the parameter types of a file's queries.</summary>
    InputModelSuffix,

    /// <summary><c>-- output-model-suffix:</c> ends the names of the row types of a file's queries.</summary>
    OutputModelSuffix,

    /// <summary><c>-- model-namespace:</c> names the namespace of the models of a file's queries.</summary>
    ModelNamespace,

    /// <summary><c>-- input-model-type:</c> gives the shape of a parameter type, for a query or for its file.</summary>
    InputModelType,

    /// <summary><c>-- output-model-type:</c> gives the shape of a row type, for a query or for its file.</summary>
    OutputModelType,

    /// <summary><c>-- input-model:</c> names the type of one query's parameters.</summary>
    InputModel,

    /// <summary><c>-- output-model:</c> names the type of one query's rows.</summary>
    OutputModel,

    /// <summary><c>-- collection-type:</c> gives the type that holds many rows, for a query or for its file.</summary>
    CollectionType,
}
