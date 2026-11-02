namespace SqlSource.Generation.Methods;

/// <summary>
/// One argument of a query's method.
/// </summary>
/// <param name="Role">What the argument is for.</param>
/// <param name="Name">
/// The identifier's value, as its documentation names it and no local may take: <c>await</c> for a token of that
/// name, which the method writes through <see cref="MethodArguments.AppendName" />.
/// </param>
/// <param name="TypeText">The argument's type, from <c>global::</c> or as a keyword.</param>
/// <param name="DefaultText">
/// The argument's default as code writes it, or null for an argument a call must give.
/// </param>
/// <param name="DocumentationXml">
/// The text of the argument's <c>param</c> as XML, or null for a token, which
/// <c>XmlDocWriter.AppendParam(builder, indent, name)</c> documents.
/// </param>
internal readonly record struct MethodArgument(
    ArgumentRole Role,
    string Name,
    string TypeText,
    string? DefaultText,
    string? DocumentationXml
);
