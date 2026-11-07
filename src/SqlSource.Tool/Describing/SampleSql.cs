using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Generation;
using SqlSource.Parsing;
using SqlSource.Parsing.Tokens;
using SqlSource.Settings;

namespace SqlSource.Tool.Describing;

/// <summary>
/// The SQL a database is asked to describe.
/// </summary>
internal static class SampleSql
{
    private const string Null = "NULL";

    private const string SubqueryOfNull = "(SELECT NULL)";

    /// <summary>
    /// The query's SQL without comments, which the hash covers, with each token's resolved default in the place of
    /// its <c>{{name:default}}</c>: nothing, for an empty one.  A list parameter is described as one value of its
    /// element: a use after <c>IN</c> is <c>(@ids)</c>.  The test of the list for <c>NULL</c> asks the server nothing
    /// of the parameter, and is a value that a planner cannot fold, whose condition stays a condition of the plan as
    /// the running query's boolean parameter does: on PostgreSQL <c>(SELECT NULL)</c>, since a folded
    /// <c>NULL IS NOT NULL OR o.total &gt; 0</c> is the strict <c>o.total &gt; 0</c>, which makes a <c>LEFT JOIN</c> on
    /// <c>o</c> inner.  In the index predicate of <c>ON CONFLICT (...) WHERE</c>, where PostgreSQL refuses a subquery
    /// and no join is reduced, it is <c>NULL</c>.  On SQL Server it is <c>NULL</c>, since SQL Server refuses a
    /// subquery in an aggregate, a <c>GROUP BY</c> and an <c>OUTPUT</c> clause, and describes a column's nullability
    /// without a plan.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="dialect">The dialect it is described in, one of <see cref="SqlDescribableDialects.All" />.</param>
    /// <exception cref="InvalidOperationException">
    /// A token has no default.  The plan gives such a query a problem, and the run does not describe it.
    /// </exception>
    public static string Build(SqlQuery query, SqlDialect dialect)
    {
        var nullTest = SqlDescribableDialects.EngineOf(dialect) switch
        {
            DatabaseEngine.PostgreSql => SubqueryOfNull,
            DatabaseEngine.SqlServer => Null,
            _ => throw UnexpectedEnumValueException.Of(dialect),
        };

        // Each null test is NULL here, and where it starts is kept, so that a stand-in of another text can replace it.
        List<int>? nullTests = null;
        var sql = new StringBuilder();
        foreach (var segment in query.Segments)
        {
            switch (segment.Kind)
            {
                case SqlSegmentKind.Literal:
                    _ = sql.Append(segment.Text);
                    break;
                case SqlSegmentKind.List:
                    _ = sql.Append('(').Append(segment.Text).Append(')');
                    break;
                case SqlSegmentKind.ListNullTest:
                    (nullTests ??= []).Add(sql.Length);
                    _ = sql.Append(Null);
                    break;
                case SqlSegmentKind.Token:
                    _ = sql.Append(DefaultOf(query, segment.Text));
                    break;
                default:
                    throw UnexpectedEnumValueException.Of(segment.Kind);
            }
        }

        var text = sql.ToString();
        return nullTests is null || nullTest == Null ? text : WithStandIns(text, nullTests, nullTest);
    }

    // The sample with the stand-in for each null test outside an index predicate, which is PostgreSQL's alone.
    private static string WithStandIns(string text, List<int> nullTests, string nullTest)
    {
        var predicates = IndexPredicates(text);
        var sample = new StringBuilder(text.Length + (nullTests.Count * (nullTest.Length - Null.Length)));
        var copied = 0;
        foreach (var start in nullTests)
        {
            _ = sample
                .Append(text, copied, start - copied)
                .Append(predicates.Exists(predicate => predicate.Contains(start)) ? Null : nullTest);
            copied = start + Null.Length;
        }

        return sample.Append(text, copied, text.Length - copied).ToString();
    }

    // Where the index predicates of ON CONFLICT ( ... ) WHERE stand in PostgreSQL's SQL: from the WHERE to the DO of
    // the conflict's action, at the WHERE's depth of parentheses.  Words are read from the text lexemes alone, so that
    // a string or a quoted name is never one.  A predicate also ends at a ")" that closes its depth, at a ";" and at
    // the end, in SQL that PostgreSQL refuses anyway.
    private static List<TextSpan> IndexPredicates(string text)
    {
        var predicates = new List<TextSpan>();
        var rules = SqlDialectRules.PostgreSql;
        var lexed = SqlLexer.Lex(text, rules);
        if (lexed.Error is not null)
        {
            return predicates;
        }

        var stage = Stage.None;
        var depth = 0;

        // The depth inside the conflict target's parentheses, and where the predicate starts and at which depth.
        var targetDepth = 0;
        var predicateStart = 0;
        var predicateDepth = 0;
        foreach (var lexeme in lexed.Lexemes)
        {
            if (lexeme.Kind is SqlLexemeKind.Quoted or SqlLexemeKind.Parameter)
            {
                Step(Item.Other, lexeme.Span);
                continue;
            }

            // A comment or a hint is white space.
            if (lexeme.Kind != SqlLexemeKind.Text)
            {
                continue;
            }

            var offset = lexeme.Span.Start;
            while (offset < lexeme.Span.End)
            {
                var start = offset;
                var value = text[offset++];
                if (rules.IsNameCharacter(value))
                {
                    while (offset < lexeme.Span.End && rules.IsNameCharacter(text[offset]))
                    {
                        offset++;
                    }

                    Step(Item.Word, TextSpan.FromBounds(start, offset));
                }
                else if (!char.IsWhiteSpace(value))
                {
                    var item = value switch
                    {
                        '(' => Item.Open,
                        ')' => Item.Close,
                        ';' => Item.Semicolon,
                        _ => Item.Other,
                    };
                    Step(item, TextSpan.FromBounds(start, offset));
                }
            }
        }

        if (stage == Stage.Predicate)
        {
            predicates.Add(TextSpan.FromBounds(predicateStart, text.Length));
        }

        return predicates;

        void Step(Item item, TextSpan span)
        {
            if (item == Item.Open)
            {
                depth++;
            }

            switch (stage)
            {
                case Stage.Target:
                    if (item == Item.Close && depth == targetDepth)
                    {
                        stage = Stage.AfterTarget;
                    }

                    break;
                case Stage.Predicate:
                    var ends =
                        depth == predicateDepth
                        && (item is Item.Close or Item.Semicolon || (item == Item.Word && Is(span, "DO")));
                    if (ends)
                    {
                        predicates.Add(TextSpan.FromBounds(predicateStart, span.Start));
                        stage = Stage.None;
                    }

                    break;
                case Stage.None:
                case Stage.On:
                case Stage.Conflict:
                case Stage.AfterTarget:
                    if (stage == Stage.Conflict && item == Item.Open)
                    {
                        stage = Stage.Target;
                        targetDepth = depth;
                    }
                    else if (stage == Stage.On && item == Item.Word && Is(span, "CONFLICT"))
                    {
                        stage = Stage.Conflict;
                    }
                    else if (stage == Stage.AfterTarget && item == Item.Word && Is(span, "WHERE"))
                    {
                        stage = Stage.Predicate;
                        predicateStart = span.End;
                        predicateDepth = depth;
                    }
                    else
                    {
                        stage = item == Item.Word && Is(span, "ON") ? Stage.On : Stage.None;
                    }

                    break;
                default:
                    throw UnexpectedEnumValueException.Of(stage);
            }

            if (item == Item.Close)
            {
                depth--;
            }
        }

        bool Is(TextSpan span, string keyword) =>
            text.AsSpan(span.Start, span.Length).Equals(keyword.AsSpan(), StringComparison.OrdinalIgnoreCase);
    }

    // Names are compared as SqlQueryHash compares them.
    private static string DefaultOf(SqlQuery query, string name)
    {
        foreach (var token in query.Tokens)
        {
            if (token.Name == name && token.Default is { } text)
            {
                return text;
            }
        }

        throw new InvalidOperationException($"The token '{name}' of the query '{query.Name}' has no default.");
    }

    // What the walk of IndexPredicates meets: a word, a parenthesis, a ";", or anything else that is not white space.
    private enum Item
    {
        Word,
        Open,
        Close,
        Semicolon,
        Other,
    }

    // How far the walk of IndexPredicates has read into an ON CONFLICT clause.
    private enum Stage
    {
        // Outside one.
        None,

        // After the word ON.
        On,

        // After ON CONFLICT.
        Conflict,

        // Inside the parentheses of the conflict target.
        Target,

        // After the conflict target's closing parenthesis.
        AfterTarget,

        // After the target's WHERE, up to the DO of the action.
        Predicate,
    }
}
