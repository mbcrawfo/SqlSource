using System.Collections.Generic;

namespace SqlSource.Repository.Tests.Documentation;

// The names of the generator parameters, in the order of GeneratorParameterList.Definitions, which a test keeps equal.
// The documents that list them, the section of SQLSRC109 and site/reference/generator-parameters.md, are checked
// against this list, so a parameter that is added is added here, and the tests name each document that misses it.
internal static class GeneratorParameterNames
{
    internal static IReadOnlyList<string> All { get; } =
    [
        "keep-comments",
        "token-validation",
        "sort-input",
        "sort-output",
        "table-models",
        "async-method-suffix",
        "array-pool",
        "list-padding",
        "collection-method",
        "transaction",
        "method-type",
    ];
}
