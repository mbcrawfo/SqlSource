namespace SqlSource.Generation.Models;

/// <summary>
/// Whether a parameter's property holds a sequence that its method sends, and so is declared as an
/// <c>IEnumerable&lt;T&gt;</c> of the element rather than as the parameter's type.
/// </summary>
internal enum ParameterSequence
{
    /// <summary>The property is declared with its type as it is, and the parameter sends its value as it is.</summary>
    None,

    /// <summary>
    /// A PostgreSQL array parameter of a <c>methods</c> query, whose type is the array <c>E[]</c>: the property is an
    /// <c>IEnumerable&lt;E&gt;</c>, and the method sends it as an <c>IList&lt;E&gt;</c>, copied into an array when
    /// it is not one.
    /// </summary>
    Array,

    /// <summary>
    /// A list parameter, whose type is its element <c>E</c>: the property is an <c>IEnumerable&lt;E&gt;</c>, and the
    /// method sends one parameter for each element.
    /// </summary>
    List,
}
