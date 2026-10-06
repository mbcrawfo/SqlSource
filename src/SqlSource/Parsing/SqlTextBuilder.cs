using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace SqlSource.Parsing;

/// <summary>
/// Builds the SQL of one block from its lexemes.
/// </summary>
internal static class SqlTextBuilder
{
    /// <summary>
    /// Builds the SQL of the lexemes from <paramref name="start" /> up to <paramref name="end" />.
    /// </summary>
    public static SqlBlockText Build(
        string text,
        EquatableArray<SqlLexeme> lexemes,
        int start,
        int end,
        bool preserveComments
    )
    {
        var writer = new Writer(text, preserveComments);
        for (var index = start; index < end; index++)
        {
            writer.Append(lexemes[index]);
        }

        return writer.Finish();
    }

    private sealed class Line
    {
        public StringBuilder Text { get; } = new();

        public List<int> Offsets { get; } = [];

        public bool IsMarker { get; set; }

        public void Append(char value, int offset)
        {
            _ = Text.Append(value);
            Offsets.Add(offset);
        }

        public void TrimEnd()
        {
            var length = Text.Length;
            while (length > 0 && Text[length - 1] is ' ' or '\t')
            {
                length--;
            }

            Offsets.RemoveRange(length, Text.Length - length);
            Text.Length = length;
        }
    }

    private sealed class Writer(string source, bool preserveComments)
    {
        private readonly List<Line> _lines = [];
        private Line _current = new();

        public void Append(SqlLexeme lexeme)
        {
            if (lexeme.Kind == SqlLexemeKind.Quoted)
            {
                AppendProtected(lexeme);
            }
            else if (SqlMarkerReader.Read(source, lexeme) is not null)
            {
                _current.IsMarker = true;
            }
            else if (preserveComments || lexeme.Kind is SqlLexemeKind.Text or SqlLexemeKind.Hint)
            {
                AppendLines(lexeme);
            }
            else if (lexeme.Kind == SqlLexemeKind.BlockComment)
            {
                // A stripped block comment leaves one space, so that the text on either side of it stays apart.
                // A stripped line comment leaves nothing.
                _current.Append(' ', lexeme.Span.Start);
            }
        }

        public SqlBlockText Finish()
        {
            EndLine();
            var kept = new List<Line>();
            foreach (var line in _lines)
            {
                line.TrimEnd();
                if (!line.IsMarker && (preserveComments || line.Text.Length > 0))
                {
                    kept.Add(line);
                }
            }

            var first = 0;
            var last = kept.Count - 1;
            while (first <= last && kept[first].Text.Length == 0)
            {
                first++;
            }

            while (last >= first && kept[last].Text.Length == 0)
            {
                last--;
            }

            return Join(kept, first, last);
        }

        private static SqlBlockText Join(List<Line> lines, int first, int last)
        {
            var text = new StringBuilder();
            var offsets = ImmutableArray.CreateBuilder<int>();
            for (var index = first; index <= last; index++)
            {
                if (index > first)
                {
                    // The offset of a joining line break is never read: a token cannot span lines.
                    _ = text.Append('\n');
                    offsets.Add(-1);
                }

                _ = text.Append(lines[index].Text);
                offsets.AddRange(lines[index].Offsets);
            }

            return new SqlBlockText(text.ToString(), offsets.ToImmutable());
        }

        // Text, hints and kept comments: a line terminator ends the output line.
        private void AppendLines(SqlLexeme lexeme)
        {
            var index = lexeme.Span.Start;
            var end = lexeme.Span.End;
            while (index < end)
            {
                var terminator = LineTerminatorLength(index, end);
                if (terminator == 0)
                {
                    _current.Append(source[index], index);
                    index++;
                }
                else
                {
                    EndLine();
                    index += terminator;
                }
            }
        }

        // Quoted regions: a line terminator becomes \n and stays inside the output line, so that the clean-up of
        // trailing blanks and blank lines never reaches into a string literal.
        private void AppendProtected(SqlLexeme lexeme)
        {
            var index = lexeme.Span.Start;
            var end = lexeme.Span.End;
            while (index < end)
            {
                var terminator = LineTerminatorLength(index, end);
                _current.Append(terminator == 0 ? source[index] : '\n', index);
                index += Math.Max(terminator, 1);
            }
        }

        private int LineTerminatorLength(int index, int end) =>
            source[index] switch
            {
                '\r' when index + 1 < end && source[index + 1] == '\n' => 2,
                '\r' or '\n' => 1,
                _ => 0,
            };

        private void EndLine()
        {
            _lines.Add(_current);
            _current = new Line();
        }
    }
}
