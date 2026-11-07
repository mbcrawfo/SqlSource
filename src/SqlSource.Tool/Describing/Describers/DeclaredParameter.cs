namespace SqlSource.Tool.Describing.Describers;

/// <summary>
/// A parameter of a query and the type its <c>-- param:</c> marker declares, as <c>SQLSRC228</c> names them.
/// </summary>
/// <param name="Written">The parameter as the SQL writes it, such as <c>@id</c>.</param>
/// <param name="Type">The type as the marker writes it, or null when it writes none.</param>
internal readonly record struct DeclaredParameter(string Written, string? Type);
