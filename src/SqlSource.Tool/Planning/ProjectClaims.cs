using System.Collections.Immutable;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Planning;

/// <summary>
/// The claims of one project, and the errors of reading them.
/// </summary>
/// <param name="Claims">The claims, in the order of the project's files and then of each file's text.</param>
/// <param name="Errors">An <c>SQLSRC208</c> for each argument that could not be read.</param>
internal sealed record ProjectClaims(ImmutableArray<TypeClaim> Claims, ImmutableArray<ToolDiagnostic> Errors);
