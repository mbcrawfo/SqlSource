using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SqlSource.Parsing;
using SqlSource.Parsing.Markers;
using SqlSource.Tool.Planning;

namespace SqlSource.Tool.Bundles;

/// <summary>
/// The <c>query.sql</c> of a bundle made from a query of a user's file: the query alone, as a file that plans the same
/// with no settings file.
/// </summary>
/// <remarks>
/// <para>
/// The file is <c>-- dialect:</c>, the dialect the query was read by, <c>-- database:</c>, the query's logical
/// database, then the query's own <c>-- name:</c> line, or one of its name for a file with no such line, and its
/// region of the file as the file holds it: its SQL and comments, its <c>-- token:</c> and <c>-- token-ignore:</c>
/// lines, and its <c>-- param:</c> lines without the C# type after <c>-&gt;</c>, which describes nothing to the
/// database.  Every other marker is dropped with its line, as is the file's header.  So the file holds only the
/// markers that <c>SqlRenamer</c>'s pass of a query file renames, and <c>BundleReplay.Plan</c> of it gives the query's
/// name, shape, SQL, tokens, parameters, hash and database, whatever came from a settings file.
/// </para>
/// <para>
/// The dialect carries no options, since a describable dialect has none.
/// </para>
/// </remarks>
internal static class BundleQueryFile
{
    /// <summary>The file of the query.</summary>
    /// <param name="query">The query, as the plan holds it.</param>
    /// <param name="fileText">The text of its file, as the plan read it: the query's spans are offsets into it.</param>
    /// <param name="dialect">The dialect the file was read by, as the plan holds it.</param>
    /// <exception cref="ArgumentException">The text is not the one the query was read from.</exception>
    public static string Of(PlannedQuery query, string fileText, SqlDialect dialect)
    {
        // Lexed as the parser lexes the file, its own -- dialect: marker in effect after the header.
        var lexer = new SqlLexer(fileText, SqlDialectRules.For(dialect));
        _ = SqlDialectMarker.Apply(lexer, fileText);
        var lexed = lexer.ReadToEnd();
        if (lexed.Error is not null || RegionOf(query, fileText, lexed.Lexemes) is not (var name, var start, var end))
        {
            throw NotTheFile(query, nameof(fileText));
        }

        var lineEnd = LineEndOf(fileText);
        var file = new StringBuilder();
        _ = file.Append("-- dialect: ").Append(SqlDialectName.Canonical(dialect)).Append(lineEnd);
        if (query.Database is { } database)
        {
            _ = file.Append("-- database: ").Append(database).Append(lineEnd);
        }

        _ = name is { } marker
            ? file.Append(fileText, marker.Span.Start, marker.Span.Length).Append(lineEnd)
            : file.Append("-- name: ").Append(query.Query.Name).Append(lineEnd);

        var rules = lexer.Rules;
        var copied = start;
        foreach (var lexeme in lexed.Lexemes)
        {
            if (
                lexeme.Span.Start < start
                || lexeme.Span.End > end
                || SqlMarkerReader.Read(fileText, lexeme) is not { } found
                || found.Kind is SqlMarkerKind.Token or SqlMarkerKind.TokenIgnore
            )
            {
                continue;
            }

            if (found.Kind == SqlMarkerKind.Param)
            {
                var value = ParamOf(fileText, rules, found) ?? throw NotTheFile(query, nameof(fileText));
                _ = file.Append(fileText, copied, found.ValueSpan.Start - copied).Append(value);
                copied = found.ValueSpan.End;
                continue;
            }

            // A marker starts its line, after blanks alone: the line goes, with its line end.
            var lineStart = LineStartOf(fileText, found.Span.Start);
            _ = file.Append(fileText, copied, lineStart - copied);
            copied = Math.Min(AfterLineEnd(fileText, found.Span.End), end);
        }

        return file.Append(fileText, copied, end - copied).ToString();
    }

    // The query's name marker, or null for a file with none, and the query's text after it: from the line after the
    // marker to the line of the next name marker, or the whole file for a file with none.  The query's name span is
    // in its marker's value.  Null when the span is in no name marker of the text.
    private static (SqlMarker? Name, int Start, int End)? RegionOf(
        PlannedQuery query,
        string text,
        EquatableArray<SqlLexeme> lexemes
    )
    {
        var nameSpan = query.Query.NameLocation.Span;
        var names = lexemes
            .Select(lexeme => SqlMarkerReader.Read(text, lexeme))
            .Where(static marker => marker is { Kind: SqlMarkerKind.Name })
            .Select(static marker => marker.GetValueOrDefault())
            .ToList();
        if (names.Count == 0)
        {
            // A file with no name marker is one query, named by its file, whose name is at the file's start.
            return nameSpan == default ? (null, 0, text.Length) : null;
        }

        var index = names.FindIndex(marker => !nameSpan.IsEmpty && marker.ValueSpan.Contains(nameSpan));
        if (index < 0)
        {
            return null;
        }

        var name = names[index];
        var end = index + 1 < names.Count ? LineStartOf(text, names[index + 1].Span.Start) : text.Length;
        return (name, AfterLineEnd(text, name.Span.End), end);
    }

    // The value of a -- param: marker as the generator reads it, without its C# type: the parameter, its type and
    // its nullability.  Null when the generator does not read it.
    private static string? ParamOf(string text, SqlDialectRules rules, SqlMarker marker) =>
        ReadMarker(text, rules, marker, static scope => scope.Declarations) is [var declaration]
            ? WriteParam(rules.ParameterPrefix, declaration.Name, declaration.Type, declaration.Nullable)
            : null;

    /// <summary>
    /// What the generator's scope of markers reads from one marker, read as the whole file's scope: the one read of a
    /// marker, which the bundle's file and the renaming of it share.
    /// </summary>
    /// <typeparam name="T">What is read.</typeparam>
    /// <param name="text">The text that holds the marker.</param>
    /// <param name="rules">The rules of the file's dialect.</param>
    /// <param name="marker">The marker.</param>
    /// <param name="read">Takes what the scope read.</param>
    /// <returns>What <paramref name="read" /> gives, or null when the scope reports an error.</returns>
    internal static T? ReadMarker<T>(string text, SqlDialectRules rules, SqlMarker marker, Func<SqlMarkerScope, T> read)
        where T : class
    {
        var errors = new List<SqlParseError>();
        var scope = new SqlMarkerScope(text, rules, SqlScopeKind.WholeFile, errors);
        scope.Read(marker);
        return errors.Count == 0 ? read(scope) : null;
    }

    /// <summary>
    /// The value of a <c>-- param:</c> marker as the generator reads it: the prefixed name, then the type when there
    /// is one, then <c>null</c> or <c>not null</c> when the nullability is given.  The one writer of it, which the
    /// bundle's file and the renaming of it share.
    /// </summary>
    /// <param name="prefix">The dialect's parameter prefix.</param>
    /// <param name="name">The parameter's name.</param>
    /// <param name="type">The database type, or null.</param>
    /// <param name="nullable">Whether the parameter is nullable, or null when the marker does not say.</param>
    internal static string WriteParam(char prefix, string name, string? type, bool? nullable)
    {
        var value = new StringBuilder().Append(prefix).Append(name);
        if (type is not null)
        {
            _ = value.Append(' ').Append(type);
        }

        return nullable switch
        {
            true => value.Append(" null").ToString(),
            false => value.Append(" not null").ToString(),
            null => value.ToString(),
        };
    }

    // The first line end of the text, so that the lines written end as the file's do; a line feed for a file of one
    // line.
    private static string LineEndOf(string text)
    {
        var end = SqlText.FindLineEnd(text, 0, text.Length);
        if (end == text.Length)
        {
            return "\n";
        }

        return text[end] == '\r' && SqlText.CharAt(text, end + 1) == '\n' ? "\r\n" : text[end].ToString();
    }

    // Where the line of a marker starts: only blanks come before a marker on its line.
    private static int LineStartOf(string text, int position)
    {
        while (position > 0 && text[position - 1] is ' ' or '\t')
        {
            position--;
        }

        return position;
    }

    // The position after the line end that ends a line comment, or the text's end.
    private static int AfterLineEnd(string text, int commentEnd)
    {
        if (commentEnd == text.Length)
        {
            return commentEnd;
        }

        return text[commentEnd] == '\r' && SqlText.CharAt(text, commentEnd + 1) == '\n'
            ? commentEnd + 2
            : commentEnd + 1;
    }

    private static ArgumentException NotTheFile(PlannedQuery query, string parameter) =>
        new($"The text is not that of the file the query '{query.Query.Name}' was read from.", parameter);
}
