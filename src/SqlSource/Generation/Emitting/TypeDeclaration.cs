namespace SqlSource.Generation.Emitting;

/// <summary>
/// What a generated partial declaration repeats of one type: enough for the compiler to join the two.
/// </summary>
/// <param name="Keyword">
/// <c>class</c>, <c>struct</c>, <c>interface</c>, <c>record</c> or <c>record struct</c>.
/// </param>
/// <param name="Name">
/// The identifier as written, for code: a keyword used as a name keeps its <c>@</c>, and a Unicode escape stays an
/// escape.
/// </param>
/// <param name="ValueName">The identifier's value, for comparisons and for the name of the generated file.</param>
/// <param name="TypeParameters">
/// The names of the type parameters as written: <c>&lt;TKey, TValue&gt;</c>.  Empty when the type is not generic.
/// </param>
/// <param name="Arity">The number of type parameters.</param>
/// <param name="RepeatsATypeParameter">
/// Whether a type parameter has the identifier of one of a type around it, which it hides, so that a name written
/// inside the type cannot reach the outer one.
/// </param>
internal sealed record TypeDeclaration(
    string Keyword,
    string Name,
    string ValueName,
    string TypeParameters,
    int Arity,
    bool RepeatsATypeParameter = false
);
