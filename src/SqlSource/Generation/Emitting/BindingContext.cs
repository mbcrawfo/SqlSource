using System.Text;

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
internal readonly record struct BindingContext(
    StringBuilder Builder,
    string Indent,
    string Command,
    string Parameter,
    string Parameters,
    ConversionNames Names
);
