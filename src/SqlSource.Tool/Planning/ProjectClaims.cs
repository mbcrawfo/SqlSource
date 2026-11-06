using System.Collections.Immutable;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Planning;

/// <summary>
/// The claims of one project, and the errors of reading them.
/// </summary>
/// <param name="Claims">The claims, in the order of the project's files and then of each file's text.</param>
/// <param name="Errors">
/// An <c>SQLSRC208</c> for each <c>Source</c> that is not a literal, and an <c>SQLSRC271</c> for each claim whose
/// <c>Source</c>, or whose type's name when there is no <c>Source</c>, is outside ASCII in a file that the compiler
/// reads in its fallback encoding.
/// </param>
internal sealed record ProjectClaims(ImmutableArray<TypeClaim> Claims, ImmutableArray<ToolDiagnostic> Errors);
