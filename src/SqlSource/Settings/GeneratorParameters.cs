using System;

namespace SqlSource.Settings;

/// <summary>
/// The generator parameters a level can name: switches, each on or off, and <see cref="Transaction" />, which takes
/// one of several words.  Most switches are off by default;
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

    /// <summary>
    /// <c>collection-method</c>: a query with parameters whose output is <c>methods</c> or <c>repository</c> also
    /// gets a collection method, which takes a collection of inputs and runs the query for each.  Off by default.
    /// </summary>
    CollectionMethod = 256,

    /// <summary>
    /// <c>transaction</c>: how a collection method wraps its runs in a transaction, one of the words of
    /// <see cref="TransactionMode" />.  It is not a switch, and is <see cref="TransactionMode.On" /> by default.
    /// </summary>
    Transaction = 512,
}
