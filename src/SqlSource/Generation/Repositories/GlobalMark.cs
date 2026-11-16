using SqlSource.Diagnostics;

namespace SqlSource.Generation.Repositories;

/// <summary>
/// A <c>[SqlSourceConnection]</c> of the project's source that may be a global connection or a broken mark: one on a
/// static member, or on a parameter.  It holds nothing that another file can change, which
/// <see cref="GlobalConnections" /> decides over the compilation.
/// </summary>
/// <param name="DeclarationId">
/// The documentation comment id of the member, or of the member that declares the parameter.  Empty for a mark that
/// is no member's, a local function, a lambda or a parameter of either, which is found again by its
/// <paramref name="Location" />.
/// </param>
/// <param name="ParameterOrdinal">The parameter's place among its member's, for a parameter; else null.</param>
/// <param name="Location">Where the marked member or parameter is declared, where a problem of it is reported.</param>
internal sealed record GlobalMark(string DeclarationId, int? ParameterOrdinal, LocationInfo Location);
