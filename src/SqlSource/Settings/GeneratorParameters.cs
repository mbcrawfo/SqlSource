using System;

namespace SqlSource.Settings;

/// <summary>
/// The switches a list of generator parameters can hold.  Each is a departure from a default, so the empty list is
/// every default.
/// </summary>
[Flags]
internal enum GeneratorParameters
{
    /// <summary>The empty list.</summary>
    None = 0,

    /// <summary><c>keep-comments</c>: comments and blank lines stay in the SQL.</summary>
    KeepComments = 1,

    /// <summary><c>no-token-validation</c>: a query's method does not check its arguments.</summary>
    NoTokenValidation = 2,

    /// <summary><c>sort-input</c>: an input model's properties are sorted by name.</summary>
    SortInput = 4,

    /// <summary><c>sort-output</c>: an output model's properties are sorted by name.</summary>
    SortOutput = 8,

    /// <summary><c>no-table-models</c>: a query whose result is a table gets a model of its own.</summary>
    NoTableModels = 16,

    /// <summary><c>async-method-suffix</c>: a generated method's name ends in <c>Async</c>.</summary>
    AsyncMethodSuffix = 32,
}
