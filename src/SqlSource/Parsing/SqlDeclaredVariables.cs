using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// The local variables that the SQL of one query declares, under a dialect that has them.
/// </summary>
/// <remarks>
/// A name is declared where it stands directly after the word <c>DECLARE</c>, or directly after a comma of the list
/// that the word starts.  The list ends at a <c>;</c> or at a word that starts a statement, and a comma or such a
/// word inside parentheses is part of an expression.  T-SQL needs no <c>;</c> between statements, so the words in
/// <see cref="StatementStarters" /> are what ends a list that has none.  Where that leaves a doubt the name stays a
/// parameter (TD-0004).  The names are read from the lexemes of the file, so nothing is lexed again, and a comment
/// among them is passed over as the SQL of the query leaves it out.
/// </remarks>
internal sealed class SqlDeclaredVariables
{
    private const string Declare = "DECLARE";

    // Every reserved word of T-SQL that starts a statement, and THROW, which is not reserved.  None can stand in an
    // expression outside parentheses.  ELSE and END are not here: they are part of CASE.
    private static readonly string[] StatementStarters =
    [
        "ALTER",
        "BACKUP",
        "BEGIN",
        "BREAK",
        "BULK",
        "CHECKPOINT",
        "CLOSE",
        "COMMIT",
        "CONTINUE",
        "CREATE",
        "DBCC",
        "DEALLOCATE",
        "DELETE",
        "DENY",
        "DROP",
        "EXEC",
        "EXECUTE",
        "FETCH",
        "GOTO",
        "GRANT",
        "IF",
        "INSERT",
        "KILL",
        "MERGE",
        "OPEN",
        "PRINT",
        "RAISERROR",
        "READTEXT",
        "RECONFIGURE",
        "RESTORE",
        "RETURN",
        "REVERT",
        "REVOKE",
        "ROLLBACK",
        "SAVE",
        "SELECT",
        "SET",
        "SETUSER",
        "SHUTDOWN",
        "THROW",
        "TRUNCATE",
        "UPDATE",
        "UPDATETEXT",
        "USE",
        "WAITFOR",
        "WHILE",
        "WITH",
        "WRITETEXT",
    ];

    private readonly string _text;
    private readonly List<TextSpan> _names;

    private SqlDeclaredVariables(string text, List<TextSpan> names)
    {
        _text = text;
        _names = names;
    }

    /// <summary>
    /// Where each declared name is in the text of the file, without its prefix.  A name may be here twice.
    /// </summary>
    public IReadOnlyList<TextSpan> Names => _names;

    /// <summary>
    /// The variables that the lexemes from <paramref name="start" /> up to <paramref name="end" /> declare, or null
    /// when they declare none.
    /// </summary>
    /// <param name="text">The text of the file.</param>
    /// <param name="lexemes">The lexemes of the file.</param>
    /// <param name="start">The first lexeme of the query.</param>
    /// <param name="end">The lexeme after the last one of the query.</param>
    /// <param name="rules">The rules the file was read by.</param>
    public static SqlDeclaredVariables? Find(
        string text,
        EquatableArray<SqlLexeme> lexemes,
        int start,
        int end,
        SqlDialectRules rules
    )
    {
        if (!rules.DeclaredVariables || start >= end)
        {
            return null;
        }

        // Most queries declare nothing, and one search says so.  Only a query that holds the word is read.
        var from = lexemes[start].Span.Start;
        if (text.IndexOf(Declare, from, lexemes[end - 1].Span.End - from, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return null;
        }

        List<TextSpan>? names = null;
        var list = default(DeclarationList);
        for (var index = start; index < end; index++)
        {
            var lexeme = lexemes[index];
            if (lexeme.Kind == SqlLexemeKind.Text)
            {
                list.Read(text, lexeme.Span);
            }
            else if (lexeme.Kind == SqlLexemeKind.Parameter && list.ExpectsName)
            {
                (names ??= []).Add(new TextSpan(lexeme.Span.Start + 1, lexeme.Span.Length - 1));
                list.ExpectsName = false;
            }
            else if (lexeme.Kind is SqlLexemeKind.Quoted or SqlLexemeKind.Parameter)
            {
                // A comment is no part of the SQL, and a hint no part of a statement.  These two end the wait for
                // a name.
                list.ExpectsName = false;
            }
        }

        return names is null ? null : new SqlDeclaredVariables(text, names);
    }

    /// <summary>Whether <paramref name="name" />, written without its prefix, is one of the variables.</summary>
    public bool Holds(ReadOnlySpan<char> name)
    {
        foreach (var variable in _names)
        {
            if (name.Equals(_text.AsSpan(variable.Start, variable.Length), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // Where the text is in a list of declarations: whether one is open, how deep in parentheses the text is since
    // its DECLARE, and whether the next thing can be a declared name.
    private struct DeclarationList
    {
        private bool _isOpen;
        private int _depth;

        public bool ExpectsName { get; set; }

        public void Read(string sql, TextSpan text)
        {
            var index = text.Start;
            while (index < text.End)
            {
                // Outside a list only the word that opens one matters, and a search finds it faster than a walk.
                if (!_isOpen)
                {
                    index = FindDeclare(sql, text, index);
                    if (index < 0)
                    {
                        return;
                    }
                }

                var value = sql[index];
                if (IsWordCharacter(value))
                {
                    var start = index;
                    while (index < text.End && IsWordCharacter(sql[index]))
                    {
                        index++;
                    }

                    ReadWord(sql.AsSpan(start, index - start));
                    continue;
                }

                index++;
                if (char.IsWhiteSpace(value))
                {
                    continue;
                }

                if (value == '(')
                {
                    _depth++;
                }
                else if (value == ')' && _depth > 0)
                {
                    _depth--;
                }
                else if (value == ';')
                {
                    _isOpen = false;
                }

                ExpectsName = value == ',' && _isOpen && _depth == 0;
            }
        }

        // "@", "#" and "$" are part of a T-SQL identifier, so "#declare" is not the word.
        private static bool IsWordCharacter(char value) =>
            SqlLexer.IsParameterNameCharacter(value) || value is '@' or '#' or '$';

        // The offset of the next whole word DECLARE of the text, from an offset on, or -1.
        private static int FindDeclare(string sql, TextSpan text, int from)
        {
            while (true)
            {
                var index = sql.IndexOf(Declare, from, text.End - from, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    return -1;
                }

                from = index + Declare.Length;
                if (
                    (index == text.Start || !IsWordCharacter(sql[index - 1]))
                    && (from == text.End || !IsWordCharacter(sql[from]))
                )
                {
                    return index;
                }
            }
        }

        private static bool IsStatementStarter(ReadOnlySpan<char> word)
        {
            foreach (var starter in StatementStarters)
            {
                if (starter.Length == word.Length && word.Equals(starter.AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void ReadWord(ReadOnlySpan<char> word)
        {
            if (word.Equals(Declare.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                _isOpen = true;
                _depth = 0;
                ExpectsName = true;
                return;
            }

            ExpectsName = false;
            if (_isOpen && _depth == 0 && IsStatementStarter(word))
            {
                _isOpen = false;
            }
        }
    }
}
