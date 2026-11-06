using System;

namespace SqlSource.Settings;

/// <summary>
/// The generator parameters: the switches a level can name, each on or off.  Most are off by default;
/// <see cref="TokenValidation" />, <see cref="TableModels" />, <see cref="ArrayPool" /> and <see cref="ListPadding" />
/// are on, and <see cref="GeneratorParameterList.Defaults" /> names them.
/// </summary>
[Flags]
internal enum GeneratorParameters
{
    /// <summary>No parameter.</summary>
    None = 0,

    /// <summary><c>keep-comments</c>: comments and blank lines stay in the SQL.</summary>
    KeepComments = 1,

    /// <summary>
    /// <c>token-validation</c>: a query's token method and query method check that each token's argument has text.
    /// On by default.
    /// </summary>
    TokenValidation = 2,

    /// <summary><c>sort-input</c>: an input model's properties are sorted by name.</summary>
    SortInput = 4,

    /// <summary><c>sort-output</c>: an output model's properties are sorted by name.</summary>
    SortOutput = 8,

    /// <summary>
    /// <c>table-models</c>: a query whose result is a table gets a model named after the table.  On by default.
    /// </summary>
    TableModels = 16,

    /// <summary><c>async-method-suffix</c>: a generated method's name ends in <c>Async</c>.</summary>
    AsyncMethodSuffix = 32,

    /// <summary>
    /// <c>array-pool</c>: a query's method reads its rows through the shared array pool.  On by default.
    /// </summary>
    ArrayPool = 64,

    /// <summary>
    /// <c>list-padding</c>: a list query parameter's values are sent padded to a fixed size.  On by default.
    /// </summary>
    ListPadding = 128,
}
