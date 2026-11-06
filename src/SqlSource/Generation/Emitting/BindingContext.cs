using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using SqlSource.Generation.Methods;

namespace SqlSource.Generation.Emitting;

/// <summary>
/// What every parameter of a query's method is bound with, which a <see cref="ParameterBinder" /> takes with each
/// parameter.  A struct, so that no binding allocates it.
/// </summary>
/// <param name="Builder">The source being built.</param>
/// <param name="Indent">The whitespace that starts each line.</param>
/// <param name="Command">The name of the command's local.</param>
/// <param name="Parameter">The name of the parameter's local.</param>
/// <param name="Parameters">The name of the argument that holds the input model.</param>
/// <param name="Names">
/// The names of what a converted parameter's binding declares, which no argument and no local of the method has.
/// </param>
/// <param name="ValueExpression">
/// The expression of the value bound, or null for the property's, <c>parameters.X</c>: a list binds each of its values
/// from its loop's element.
/// </param>
/// <param name="NameExpression">
/// The expression of the parameter's name, or null for the literal of the parameter's own: a list names each of its
/// values by its prefix and the value's index.
/// </param>
/// <param name="IsNull">
/// Whether the value bound is always a null, the one value of an empty list, which a SQL Server binding then neither
/// checks for its length nor rounds.
/// </param>
internal readonly record struct BindingContext(
    StringBuilder Builder,
    string Indent,
    string Command,
    string Parameter,
    string Parameters,
    ConversionNames Names,
    string? ValueExpression = null,
    string? NameExpression = null,
    bool IsNull = false
)
{
    /// <summary>The value bound: <see cref="ValueExpression" />, or the property's, <c>parameters.X</c>.</summary>
    public string ValueOf(MethodParameter bound) => ValueExpression ?? (Parameters + "." + bound.Property);

    /// <summary>The name bound: <see cref="NameExpression" />, or the literal of the parameter's own name.</summary>
    public string NameOf(MethodParameter bound) =>
        NameExpression ?? SymbolDisplay.FormatLiteral(bound.Name, quote: true);
}
