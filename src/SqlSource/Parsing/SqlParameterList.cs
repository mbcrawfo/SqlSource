using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// Builds the parameter list of one query.
/// </summary>
/// <remarks>
/// The list is the parameters of the query's static SQL in order of first appearance, then the parameters that a
/// <c>-- param:</c> marker declares and the static SQL does not hold, in marker order.  Static means outside every
/// token: a parameter inside <c>{{name:default}}</c> is part of a sample.  Names compare ignoring case.  A name that
/// the SQL declares as a local variable is not a parameter at any place: <see cref="SqlDeclaredVariables" />.
/// </remarks>
internal static class SqlParameterList
{
    /// <summary>
    /// Reports a declared parameter that only a token can bring and that has no type, and a parameter that stands
    /// in the default of one of the query's tokens and is neither in the static SQL nor declared.
    /// </summary>
    public static EquatableArray<SqlQueryParameter> Create(
        SqlDialectRules rules,
        SqlBlockText sql,
        SqlDeclaredVariables? variables,
        EquatableArray<SqlTokenOccurrence> occurrences,
        EquatableArray<SqlToken> tokens,
        IReadOnlyList<SqlTokenDefault> markerDefaults,
        IReadOnlyList<SqlParameterDeclaration> declarations,
        List<SqlParseError> errors
    )
    {
        if (sql.Parameters.Length == 0 && declarations.Count == 0 && markerDefaults.Count == 0)
        {
            return EquatableArray<SqlQueryParameter>.Empty;
        }

        var parameters = ImmutableArray.CreateBuilder<SqlQueryParameter>();

        // The parameters of the defaults.  A name is a string only where it is reported.
        List<DefaultParameter>? inDefaults = null;
        var token = 0;
        foreach (var span in sql.Parameters)
        {
            // Both lists are in the order of the SQL.
            while (token < occurrences.Count && occurrences[token].Span.End <= span.Start)
            {
                token++;
            }

            if (variables?.Holds(sql.Text.AsSpan(span.Start + 1, span.Length - 1)) == true)
            {
                continue;
            }

            if (token < occurrences.Count && occurrences[token].Span.Start <= span.Start)
            {
                (inDefaults ??= []).Add(new DefaultParameter(sql.Text, span, -1));
            }
            else if (IndexOf(parameters, sql.Text.AsSpan(span.Start + 1, span.Length - 1)) < 0)
            {
                parameters.Add(new SqlQueryParameter(NameOf(sql.Text, span), null, null, false));
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
            ReportUndeclared(parameters, inDefaults, variables, sql, rules, errors);
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

    // A declared parameter that the SQL holds takes its type; one that it does not hold is added after the others.
    private static void Declare(
        ImmutableArray<SqlQueryParameter>.Builder parameters,
        SqlParameterDeclaration declaration,
        SqlDialectRules rules,
        List<SqlParseError> errors
    )
    {
        var index = IndexOf(parameters, declaration.Name.AsSpan());
        if (index >= 0)
        {
            parameters[index] = parameters[index] with
            {
                Type = declaration.Type,
                Nullable = declaration.Nullable,
                IsDeclared = true,
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

        parameters.Add(new SqlQueryParameter(declaration.Name, declaration.Type, declaration.Nullable, true));
    }

    private static void ReportUndeclared(
        ImmutableArray<SqlQueryParameter>.Builder parameters,
        List<DefaultParameter> inDefaults,
        SqlDeclaredVariables? variables,
        SqlBlockText sql,
        SqlDialectRules rules,
        List<SqlParseError> errors
    )
    {
        foreach (var parameter in inDefaults)
        {
            var name = parameter.Text.AsSpan(parameter.Span.Start + 1, parameter.Span.Length - 1);
            if (IndexOf(parameters, name) >= 0 || variables?.Holds(name) == true)
            {
                continue;
            }

            errors.Add(
                SqlParseError.Create(
                    SqlParseErrorKind.UndeclaredParameter,
                    parameter.Offset < 0
                        ? sql.ToSourceSpan(parameter.Span)
                        : new TextSpan(parameter.Offset + parameter.Span.Start, parameter.Span.Length),
                    rules.ParameterPrefix + NameOf(parameter.Text, parameter.Span)
                )
            );
        }
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

    // The span holds the prefix, which is one character.
    private static string NameOf(string text, TextSpan span) => text.Substring(span.Start + 1, span.Length - 1);

    private static int IndexOf(ImmutableArray<SqlQueryParameter>.Builder parameters, ReadOnlySpan<char> name)
    {
        for (var index = 0; index < parameters.Count; index++)
        {
            if (name.Equals(parameters[index].Name.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    // A parameter inside a default: in the SQL (Offset is -1) or in the text of a marker's default, which starts at
    // Offset in the file.  Span is the parameter in Text, prefix included.
    private readonly record struct DefaultParameter(string Text, TextSpan Span, int Offset);
}
