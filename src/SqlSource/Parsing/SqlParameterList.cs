using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Parsing.Markers;
using SqlSource.Parsing.Tokens;

namespace SqlSource.Parsing;

/// <summary>
/// Builds the parameter list of one query.
/// </summary>
/// <remarks>
/// The list is the parameters of the query's static SQL in order of first appearance, then the parameters that a
/// <c>-- param:</c> marker declares and the static SQL does not hold, in marker order.  Static means outside every
/// token: a parameter inside <c>{{name:default}}</c> is part of a sample.  Names compare ignoring case.  A name that
/// the SQL declares as a local variable is not a parameter at any place: <see cref="SqlDeclaredVariables" />.  The
/// list keeps the spelling of a name that the SQL writes first.  In SQL Server, where an instance may compare names
/// by case, each further spelling of a name that the SQL or a token's default writes is a warning, once for each
/// spelling in a query, at its first place in the file; a marker's spelling is not.
/// </remarks>
internal static class SqlParameterList
{
    /// <summary>
    /// Reports a declared parameter that only a token can bring and that has no type, and a parameter that stands
    /// in the default of one of the query's tokens and is neither in the static SQL nor declared.  Warns of a
    /// parameter of a SQL Server query that is written in a second spelling.
    /// </summary>
    public static EquatableArray<SqlQueryParameter> Create(
        SqlDialectRules rules,
        SqlScannedQuery query,
        IReadOnlyList<SqlTokenDefault> markerDefaults,
        IReadOnlyList<SqlParameterDeclaration> declarations,
        List<SqlParseError> errors,
        List<SqlParseError> warnings
    )
    {
        var (sql, occurrences, tokens, variables) = query;
        if (sql.Parameters.Length == 0 && declarations.Count == 0 && markerDefaults.Count == 0)
        {
            return EquatableArray<SqlQueryParameter>.Empty;
        }

        var parameters = ImmutableArray.CreateBuilder<SqlQueryParameter>();

        // The parameters of the defaults.  A name is a string only where it is reported.
        List<DefaultParameter>? inDefaults = null;

        // The spellings that a warning has named, each with its warning's place in warnings; made on the first.
        List<(string Spelling, int Warning)>? warned = null;
        var warnsOfSpellings = rules.Dialect == SqlDialect.SqlServer;
        var token = 0;
        foreach (var span in sql.Parameters)
        {
            // Both lists are in the order of the SQL.
            while (token < occurrences.Count && occurrences[token].Span.End <= span.Start)
            {
                token++;
            }

            var name = SqlParameterName.Of(span);
            if (variables?.Holds(sql.Text.AsSpan(name.Start, name.Length)) == true)
            {
                continue;
            }

            if (token < occurrences.Count && occurrences[token].Span.Start <= span.Start)
            {
                (inDefaults ??= []).Add(new DefaultParameter(sql.Text, span, -1));
            }
            else
            {
                var written = sql.Text.AsSpan(name.Start, name.Length);
                var index = SqlParameterName.IndexOf(parameters, written);
                if (index < 0)
                {
                    parameters.Add(new SqlQueryParameter(NameOf(sql.Text, span), null, null, false));
                }
                else if (warnsOfSpellings)
                {
                    WarnOfSpelling(ref warned, warnings, rules, written, parameters[index], sql.ToSourceSpan(span));
                }
            }
        }

        // An index loop: the list is an interface, and an enumerator of it would be allocated for each query.
        for (var position = 0; position < markerDefaults.Count; position++)
        {
            var marker = markerDefaults[position];
            if (Holds(tokens, marker.Name))
            {
                AddParameters(ref inDefaults, marker, rules);
            }
        }

        for (var position = 0; position < declarations.Count; position++)
        {
            Declare(parameters, declarations[position], rules, errors);
        }

        if (inDefaults is not null)
        {
            ReportUndeclared(parameters, inDefaults, variables, sql, rules, errors, warnings, ref warned);
        }

        return parameters.Count == 0
            ? EquatableArray<SqlQueryParameter>.Empty
            : new EquatableArray<SqlQueryParameter>(parameters.ToImmutable());
    }

    private static void AddParameters(
        ref List<DefaultParameter>? inDefaults,
        SqlTokenDefault marker,
        SqlDialectRules rules
    )
    {
        foreach (var lexeme in SqlLexer.Lex(marker.Text, rules).Lexemes)
        {
            if (lexeme.Kind == SqlLexemeKind.Parameter)
            {
                (inDefaults ??= []).Add(new DefaultParameter(marker.Text, lexeme.Span, marker.Offset));
            }
        }
    }

    // A declared parameter that the SQL holds takes its types; one that it does not hold is added after the others.
    private static void Declare(
        ImmutableArray<SqlQueryParameter>.Builder parameters,
        SqlParameterDeclaration declaration,
        SqlDialectRules rules,
        List<SqlParseError> errors
    )
    {
        var index = SqlParameterName.IndexOf(parameters, declaration.Name.AsSpan());
        if (index >= 0)
        {
            parameters[index] = parameters[index] with
            {
                Type = declaration.Type,
                Nullable = declaration.Nullable,
                IsDeclared = true,
                ClrType = declaration.ClrType,
            };
            return;
        }

        if (declaration.Type is null)
        {
            errors.Add(
                SqlParseError.Create(
                    SqlParseErrorKind.MissingParameterType,
                    declaration.Marker.ValueSpan,
                    rules.ParameterPrefix + declaration.Name
                )
            );
        }

        parameters.Add(
            new SqlQueryParameter(declaration.Name, declaration.Type, declaration.Nullable, true, declaration.ClrType)
        );
    }

    private static void ReportUndeclared(
        ImmutableArray<SqlQueryParameter>.Builder parameters,
        List<DefaultParameter> inDefaults,
        SqlDeclaredVariables? variables,
        SqlBlockText sql,
        SqlDialectRules rules,
        List<SqlParseError> errors,
        List<SqlParseError> warnings,
        ref List<(string Spelling, int Warning)>? warned
    )
    {
        foreach (var parameter in inDefaults)
        {
            var nameSpan = SqlParameterName.Of(parameter.Span);
            var name = parameter.Text.AsSpan(nameSpan.Start, nameSpan.Length);
            if (variables?.Holds(name) == true)
            {
                continue;
            }

            var index = SqlParameterName.IndexOf(parameters, name);

            var span =
                parameter.Offset < 0
                    ? sql.ToSourceSpan(parameter.Span)
                    : new TextSpan(parameter.Offset + parameter.Span.Start, parameter.Span.Length);
            if (index < 0)
            {
                errors.Add(
                    SqlParseError.Create(
                        SqlParseErrorKind.UndeclaredParameter,
                        span,
                        rules.ParameterPrefix + NameOf(parameter.Text, parameter.Span)
                    )
                );
            }
            else if (rules.Dialect == SqlDialect.SqlServer)
            {
                WarnOfSpelling(ref warned, warnings, rules, name, parameters[index], span);
            }
        }
    }

    // A warning when the parameter is written in a spelling other than the list's.  A spelling is warned of once in a
    // query, at its first place in the file, which is not the order the places are found in.  Nothing is allocated for
    // a parameter that is written as the list has it.
    private static void WarnOfSpelling(
        ref List<(string Spelling, int Warning)>? warned,
        List<SqlParseError> warnings,
        SqlDialectRules rules,
        ReadOnlySpan<char> written,
        SqlQueryParameter listed,
        TextSpan span
    )
    {
        if (written.Equals(listed.Name.AsSpan(), StringComparison.Ordinal))
        {
            return;
        }

        if (warned is not null)
        {
            foreach (var (spelling, warning) in warned)
            {
                if (written.Equals(spelling.AsSpan(), StringComparison.Ordinal))
                {
                    if (span.Start < warnings[warning].Span.Start)
                    {
                        warnings[warning] = warnings[warning] with { Span = span };
                    }

                    return;
                }
            }
        }

        var text = written.ToString();
        (warned ??= []).Add((text, warnings.Count));
        warnings.Add(
            SqlParseError.Create(
                SqlParseErrorKind.ParameterInTwoSpellings,
                span,
                rules.ParameterPrefix + text,
                rules.ParameterPrefix + listed.Name
            )
        );
    }

    private static bool Holds(EquatableArray<SqlToken> tokens, string name)
    {
        for (var index = 0; index < tokens.Count; index++)
        {
            if (string.Equals(tokens[index].Name, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string NameOf(string text, TextSpan span)
    {
        var name = SqlParameterName.Of(span);
        return text.Substring(name.Start, name.Length);
    }

    // A parameter inside a default: in the SQL (Offset is -1) or in the text of a marker's default, which starts at
    // Offset in the file.  Span is the parameter in Text, prefix included.
    private readonly record struct DefaultParameter(string Text, TextSpan Span, int Offset);
}
