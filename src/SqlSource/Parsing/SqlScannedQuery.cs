using System.Collections.Immutable;
using SqlSource.Parsing.Tokens;

namespace SqlSource.Parsing;

/// <summary>
/// The SQL of one query as built, with what was read from it.  Each is found from the one before, and
/// <see cref="SqlParameterList" /> reads them together.
/// </summary>
/// <param name="Sql">The SQL without comments, with where each parameter is in it.</param>
/// <param name="Occurrences">Where each token is in the SQL, in order.</param>
/// <param name="Tokens">The query's tokens, each name once.</param>
/// <param name="Variables">
/// The names the SQL declares as local variables, or null when the dialect has none, the SQL has no parameter or it
/// declares none.
/// </param>
/// <param name="Uses">
/// What each parameter of the SQL is used as, in the order of <see cref="SqlBlockText.Parameters" />, or empty when
/// every use is a value.
/// </param>
internal readonly record struct SqlScannedQuery(
    SqlBlockText Sql,
    EquatableArray<SqlTokenOccurrence> Occurrences,
    EquatableArray<SqlToken> Tokens,
    SqlDeclaredVariables? Variables,
    ImmutableArray<SqlParameterUse> Uses
);
