using SqlSource.Generation.Emitting.QueryMethods;

namespace SqlSource.Generation.Emitting;

/// <summary>
/// The names that a query's method gives what its conversions declare, none of which an argument or a local of the
/// method has, from its <see cref="LocalNames" />.  A name is empty when no conversion of the method declares it.
/// </summary>
/// <param name="Value">
/// The pattern variable that holds the value of a converted parameter, or of an array that its property holds as any
/// sequence: <c>value</c>.
/// </param>
/// <param name="Element">The parameter of the static lambda that converts an array's element: <c>element</c>.</param>
/// <param name="Item">The pattern variable that holds an element that is not null, in that lambda: <c>item</c>.</param>
internal sealed record ConversionNames(string Value, string Element, string Item);
