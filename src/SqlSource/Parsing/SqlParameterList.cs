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
/// list keeps the spelling of a name that the SQL writes first outside tokens; a parameter that only a marker brings
/// keeps the marker's spelling, and the spellings in token defaults are compared against it.  In SQL Server, where a
/// server may compare names by case, each further spelling of a name that the SQL or a token's default writes is a
/// warning, once for each spelling in a query, at its first place in the file; a marker's spelling is never itself
/// warned of.  A parameter that the static SQL writes directly after <c>IN</c> is a list, in every spelling, and every
/// other use of it in the static SQL must be another such use or a null test.  A token's default is the caller's text
/// at run time, where no list can be expanded, so no list, and no parameter directly after <c>IN</c>, may stand there.
/// </remarks>
internal static class SqlParameterList
{
    private const string AsOneValue = "is used here as one value";

    private const string InATokensDefault = "stands here in a token's default, which the method cannot expand";

    private const string WithJson = "has a -- param: marker that says json, which a list cannot hold";

    /// <summary>
    /// Reports a declared parameter that only a token can bring and that has no type, a parameter that stands in the
    /// default of one of the query's tokens and is neither in the static SQL nor declared, and a list that stands
    /// where a list cannot.  Warns of a parameter of a SQL Server query that is written in a second spelling.
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
        var (sql, occurrences, tokens, variables, uses) = query;
        if (sql.Parameters.Length == 0 && declarations.Count == 0 && markerDefaults.Count == 0)
        {
            return EquatableArray<SqlQueryParameter>.Empty;
        }

        var parameters = ImmutableArray.CreateBuilder<SqlQueryParameter>();

        // The parameters of the defaults.  A name is a string only where it is reported.
        List<DefaultParameter>? inDefaults = null;

        // The spellings that a warning has named, each with its warning's place in warnings; made on the first.
        List<(string Spelling, int Warning)>? warned = null;

        // The uses of the static SQL, each with its parameter's place in the list, when any use is a list.
        var staticUses = uses.IsEmpty ? null : new List<StaticUse>(uses.Length);
        var warnsOfSpellings = rules.Dialect == SqlDialect.SqlServer;
        var token = 0;
        for (var position = 0; position < sql.Parameters.Length; position++)
        {
            var span = sql.Parameters[position];

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

            var use = uses.IsEmpty ? SqlParameterUse.Value : uses[position];
            if (token < occurrences.Count && occurrences[token].Span.Start <= span.Start)
            {
                (inDefaults ??= []).Add(new DefaultParameter(sql.Text, span, -1, use));
            }
            else
            {
                var written = sql.Text.AsSpan(name.Start, name.Length);
                var index = SqlParameterName.IndexOf(parameters, written);
                if (index < 0)
                {
                    index = parameters.Count;
                    parameters.Add(new SqlQueryParameter(NameOf(sql.Text, span), null, null, false));
                }
                else if (warnsOfSpellings)
                {
                    WarnOfSpelling(ref warned, warnings, rules, written, parameters[index], sql.ToSourceSpan(span));
                }

                staticUses?.Add(new StaticUse(index, use, span));
            }
        }

        if (staticUses is not null)
        {
            MarkLists(parameters, staticUses, sql, rules, errors);
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
            CheckDefaults(parameters, inDefaults, variables, sql, rules, errors, warnings, ref warned);
        }

        return parameters.Count == 0
            ? EquatableArray<SqlQueryParameter>.Empty
            : new EquatableArray<SqlQueryParameter>(parameters.ToImmutable());
    }

    // A list is a parameter that has a list use.  Its other uses must be list uses or null tests, and each other use is
    // reported where it is written.
    private static void MarkLists(
        ImmutableArray<SqlQueryParameter>.Builder parameters,
        List<StaticUse> uses,
        SqlBlockText sql,
        SqlDialectRules rules,
        List<SqlParseError> errors
    )
    {
        foreach (var use in uses)
        {
            if (use.Use == SqlParameterUse.List && !parameters[use.Parameter].IsList)
            {
                parameters[use.Parameter] = parameters[use.Parameter] with { IsList = true };
            }
        }

        foreach (var use in uses)
        {
            var parameter = parameters[use.Parameter];
            if (!parameter.IsList)
            {
                continue;
            }

            switch (use.Use)
            {
                case SqlParameterUse.List:
                    break;
                case SqlParameterUse.NullTest:
                    if (!parameter.HasNullTest)
                    {
                        parameters[use.Parameter] = parameter with { HasNullTest = true };
                    }

                    break;
                case SqlParameterUse.Value:
                    errors.Add(
                        SqlParseError.Create(
                            SqlParseErrorKind.ListParameterMisused,
                            sql.ToSourceSpan(use.Span),
                            rules.ParameterPrefix + NameOf(sql.Text, use.Span),
                            AsOneValue
                        )
                    );
                    break;
                default:
                    throw UnexpectedEnumValueException.Of(use.Use);
            }
        }
    }

    // The marker's text is lexed on its own, so its uses are read from that lexing.
    private static void AddParameters(
        ref List<DefaultParameter>? inDefaults,
        SqlTokenDefault marker,
        SqlDialectRules rules
    )
    {
        var lexemes = SqlLexer.Lex(marker.Text, rules).Lexemes;
        var uses = SqlListUses.Find(marker.Text, lexemes, 0, lexemes.Count, rules);
        var position = 0;
        foreach (var lexeme in lexemes)
        {
            if (lexeme.Kind == SqlLexemeKind.Parameter)
            {
                var use = uses.IsEmpty ? SqlParameterUse.Value : uses[position];
                position++;
                (inDefaults ??= []).Add(new DefaultParameter(marker.Text, lexeme.Span, marker.Offset, use));
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
            if (declaration.IsJson && parameters[index].IsList)
            {
                errors.Add(
                    SqlParseError.Create(
                        SqlParseErrorKind.ListParameterMisused,
                        declaration.Marker.ValueSpan,
                        rules.ParameterPrefix + declaration.Name,
                        WithJson
                    )
                );
            }

            parameters[index] = parameters[index] with
            {
                Type = declaration.Type,
                Nullable = declaration.Nullable,
                IsDeclared = true,
                ClrType = declaration.ClrType,
                IsJson = declaration.IsJson,
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
            new SqlQueryParameter(
                declaration.Name,
                declaration.Type,
                declaration.Nullable,
                true,
                declaration.ClrType,
                declaration.IsJson
            )
        );
    }

    // Reports a parameter of a default that the list does not hold, and a list use or a list in a default.  The list
    // is complete by now, declared parameters included.
    private static void CheckDefaults(
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

            if (parameter.Use == SqlParameterUse.List || (index >= 0 && parameters[index].IsList))
            {
                errors.Add(
                    SqlParseError.Create(
                        SqlParseErrorKind.ListParameterMisused,
                        span,
                        rules.ParameterPrefix + NameOf(parameter.Text, parameter.Span),
                        InATokensDefault
                    )
                );
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
    // Offset in the file.  Span is the parameter in Text, prefix included, and Use what the default uses it as.
    private readonly record struct DefaultParameter(string Text, TextSpan Span, int Offset, SqlParameterUse Use);

    // A use of a parameter in the static SQL.  Parameter is its place in the list, and Span its place in the SQL,
    // prefix included.
    private readonly record struct StaticUse(int Parameter, SqlParameterUse Use, TextSpan Span);
}
