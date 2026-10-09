using System.Buffers;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// Builds the SQL of one block from its lexemes.
/// </summary>
/// <remarks>
/// This is where a parse does most of its work, so it is written to allocate little.  The output is written once into
/// a pooled buffer, text is copied from the file in runs, and nothing is allocated for a line or for a character.
/// </remarks>
internal static class SqlTextBuilder
{
    private static readonly char[] LineTerminators = ['\r', '\n'];

    /// <summary>
    /// Builds the SQL of the lexemes from <paramref name="start" /> up to <paramref name="end" />.
    /// </summary>
    public static SqlBlockText Build(
        string text,
        EquatableArray<SqlLexeme> lexemes,
        int start,
        int end,
        bool keepComments
    )
    {
        if (start >= end)
        {
            return new SqlBlockText(string.Empty, [], []);
        }

        // The output is never longer than its source: each character written stands for a different character read.
        var buffer = ArrayPool<char>.Shared.Rent(lexemes[end - 1].Span.End - lexemes[start].Span.Start);
        try
        {
            var writer = new Writer(text, keepComments, buffer);
            for (var index = start; index < end; index++)
            {
                writer.Append(lexemes[index]);
            }

            return writer.Finish();
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    // The buffer holds the finished output, then the line breaks that would join the next line to it, then the line
    // being written.  A line is judged when it ends: kept with its trailing blanks trimmed, or dropped by moving back
    // to the end of the finished output.
    private sealed class Writer(string source, bool keepComments, char[] buffer)
    {
        private readonly List<(int Output, int Source)> _runs = [];

        // Created at the first parameter: most blocks have none.
        private List<TextSpan>? _parameters;

        // The length of the finished output: whole lines, with no line break after the last one.
        private int _finished;

        // Where the line being written starts and where its content ends.  Blanks after the content are trimmed if the
        // line ends there.  A line whose content is empty is blank.
        private int _lineStart;
        private int _contentEnd;
        private int _position;

        // Blank lines met since the last finished line.  They are written only if another line follows them.
        private int _pendingBlankLines;
        private bool _isMarkerLine;

        public void Append(SqlLexeme lexeme)
        {
            if (lexeme.Kind is SqlLexemeKind.Quoted or SqlLexemeKind.Hint)
            {
                AppendProtected(lexeme);
            }
            else if (SqlMarkerReader.Read(source, lexeme) is not null)
            {
                _isMarkerLine = true;
            }
            else if (lexeme.Kind == SqlLexemeKind.Parameter)
            {
                // A parameter is on a line with content and never on a marker's line, so the line is kept and the
                // offset stands.
                (_parameters ??= []).Add(new TextSpan(_position, lexeme.Span.Length));
                AppendLines(lexeme);
            }
            else if (keepComments || lexeme.Kind == SqlLexemeKind.Text)
            {
                AppendLines(lexeme);
            }
            else if (lexeme.Kind == SqlLexemeKind.BlockComment)
            {
                // A stripped block comment leaves one space, so that the text on either side of it stays apart.
                // A stripped line comment leaves nothing.
                Write(' ', lexeme.Span.Start);
            }
        }

        public SqlBlockText Finish()
        {
            EndLine();
            return new SqlBlockText(
                new string(buffer, 0, _finished),
                [.. _runs],
                _parameters is null ? [] : [.. _parameters]
            );
        }

        // Text and kept comments: a line terminator ends the output line.
        private void AppendLines(SqlLexeme lexeme)
        {
            var index = lexeme.Span.Start;
            var end = lexeme.Span.End;
            while (index < end)
            {
                var lineEnd = FindLineEnd(index, end);
                Copy(index, lineEnd);
                ExtendContent(index, lineEnd);
                if (lineEnd < end)
                {
                    EndLine();
                    BeginLine();
                }

                index = lineEnd + TerminatorLength(lineEnd, end);
            }
        }

        // Quoted regions and hints: a line terminator becomes \n and stays inside the output line, and every character
        // counts as content, so that the clean-up of trailing blanks and blank lines never reaches into a string
        // literal.  A hint is protected because it can hold executable SQL, string literals included (MySQL's
        // /*! ... */).
        private void AppendProtected(SqlLexeme lexeme)
        {
            var index = lexeme.Span.Start;
            var end = lexeme.Span.End;
            while (index < end)
            {
                var lineEnd = FindLineEnd(index, end);
                Copy(index, lineEnd);
                if (lineEnd < end)
                {
                    Write('\n', lineEnd);
                }

                index = lineEnd + TerminatorLength(lineEnd, end);
            }

            _contentEnd = _position;
        }

        private int FindLineEnd(int start, int end)
        {
            var terminator = source.IndexOfAny(LineTerminators, start, end - start);
            return terminator < 0 ? end : terminator;
        }

        private int TerminatorLength(int index, int end)
        {
            if (index == end)
            {
                return 0;
            }

            return source[index] == '\r' && index + 1 < end && source[index + 1] == '\n' ? 2 : 1;
        }

        private void Copy(int start, int end)
        {
            if (end > start)
            {
                _runs.Add((_position, start));
                source.CopyTo(start, buffer, _position, end - start);
                _position += end - start;
            }
        }

        private void Write(char value, int sourceOffset)
        {
            _runs.Add((_position, sourceOffset));
            buffer[_position] = value;
            _position++;
        }

        // Moves the end of the line's content to just after the last character of the copied text that is not a blank.
        private void ExtendContent(int start, int end)
        {
            var last = end - 1;
            while (last >= start && source[last] is ' ' or '\t')
            {
                last--;
            }

            if (last >= start)
            {
                _contentEnd = _position - (end - 1 - last);
            }
        }

        // Keeps the line being written, without its trailing blanks, or drops it.
        private void EndLine()
        {
            if (!_isMarkerLine && _contentEnd > _lineStart)
            {
                RemoveRunsFrom(_contentEnd);
                _finished = _contentEnd;
                _pendingBlankLines = 0;
                return;
            }

            RemoveRunsFrom(_lineStart);
            if (!_isMarkerLine && keepComments && _finished > 0)
            {
                _pendingBlankLines++;
            }
        }

        // Starts a line after the finished output, joined to it by a line break and by one more for each pending blank
        // line.  If the line is dropped, the next one starts from the same place.
        private void BeginLine()
        {
            _position = _finished;
            if (_finished > 0)
            {
                for (var count = 0; count <= _pendingBlankLines; count++)
                {
                    buffer[_position] = '\n';
                    _position++;
                }
            }

            _lineStart = _position;
            _contentEnd = _position;
            _isMarkerLine = false;
        }

        private void RemoveRunsFrom(int output)
        {
            while (_runs.Count > 0 && _runs[_runs.Count - 1].Output >= output)
            {
                _runs.RemoveAt(_runs.Count - 1);
            }
        }
    }
}
