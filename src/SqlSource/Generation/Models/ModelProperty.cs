using SqlSource.Generation.Methods;
using SqlSource.Generation.Types;

namespace SqlSource.Generation.Models;

/// <summary>
/// One property of a model: a parameter of the query in its input model, or a column in its output model.
/// </summary>
/// <param name="Name">The property's name, the PascalCase of the parameter's or the column's.</param>
/// <param name="Type">
/// The property's C# type, without the <c>?</c> that <paramref name="IsNullable" /> adds: the type the parameter or
/// column maps to, which <paramref name="Sequence" /> may declare as a sequence of its element.
/// </param>
/// <param name="IsNullable">Whether the property can be null.</param>
/// <param name="Documentation">The property's documentation, as escaped XML.</param>
/// <param name="Subject">The parameter or the column as a message names it: <c>column 'id'</c>.</param>
/// <param name="IsArray">
/// Whether the column's PostgreSQL type is an array, or a domain whose base type is one.  Always false for a parameter.
/// It changes no property's C#, so <c>ModelCollector.FindDifference</c> does not compare it.
/// </param>
/// <param name="Sequence">
/// Whether the property is declared as an <c>IEnumerable&lt;T&gt;</c> of its element, which <see cref="DeclaredType" />
/// writes.  Always <see cref="ParameterSequence.None" /> for a column.
/// </param>
internal sealed record ModelProperty(
    string Name,
    PropertyType Type,
    bool IsNullable,
    string Documentation,
    string Subject,
    bool IsArray,
    ParameterSequence Sequence = ParameterSequence.None
)
{
    private const string Enumerable = "global::System.Collections.Generic.IEnumerable<";

    /// <summary>
    /// The C# type the property is declared with, without the <c>?</c> that <see cref="IsNullable" /> adds:
    /// <see cref="PropertyType.Text" />, or an <c>IEnumerable&lt;T&gt;</c> of its element for a sequence, of
    /// <c>int</c> for both the array <c>int[]</c> and the list of <c>int</c>.  It is computed, so it takes no part in
    /// equality.
    /// </summary>
    public string DeclaredType =>
        Sequence switch
        {
            ParameterSequence.None => Type.Text,
            ParameterSequence.Array => Enumerable + (ConversionPlanner.ElementOf(Type.Text) ?? Type.Text) + ">",
            ParameterSequence.List => Enumerable + Type.Text + ">",
            _ => throw UnexpectedEnumValueException.Of(Sequence),
        };
}
