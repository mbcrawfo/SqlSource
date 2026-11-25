using System.Collections.Concurrent;
using SqlSource.Parsing.Quoting;

namespace SqlSource.Parsing;

/// <summary>
/// How one dialect reads comments and quoted regions.  The lexer reads by the rules it is given and names no dialect.
/// </summary>
/// <remarks>
/// Every dialect-sensitive choice is here or in a <see cref="QuoteReader" />.  The static properties, and the rules
/// of MySQL and MariaDB under each set of options, are the whole table: a value where dialects differ by a setting, a
/// reader where they differ by how a quoted region ends.  Changing a cell changes the SQL users get.
/// </remarks>
internal sealed class SqlDialectRules
{
    private const int TableSize = 128;

    // One for each combination of SqlDialectOptions.
    private const int OptionSets = 4;

    // The first characters of --, # and /*.
    private const string CommentStarters = "-#/";

    // The prefix of a parameter in every dialect SqlSource reads today.  It is a value of the rules so that an engine
    // with another prefix, Oracle's ":name", is a rule and not a rewrite.
    private const char AtSign = '@';

    private static readonly QuoteReader Doubled = new DoubledQuoteReader();

    private static readonly QuoteReader Backslash = new BackslashQuoteReader();

    private static readonly QuoteReader EscapeString = new EscapeStringReader("Ee");

    // CockroachDB's bytes literal, b'...', takes backslash escapes as an E string does.
    private static readonly QuoteReader BytesEscapeString = new EscapeStringReader("Eeb");

    private static readonly QuoteReader QuoteOperator = new QuoteOperatorReader();

    private static readonly QuoteReader Bracket = new BracketReader(doubledCloserEscapes: false);

    private static readonly QuoteReader EscapedBracket = new BracketReader(doubledCloserEscapes: true);

    private static readonly QuoteReader Dollar = new DollarQuoteReader();

    // The rules of MySQL and of MariaDB under each set of options, at the index that the options have as a number.
    private static readonly SqlDialectRules[] MySqlByOptions = CreateMySqlFamily(isMariaDb: false);

    private static readonly SqlDialectRules[] MariaDbByOptions = CreateMySqlFamily(isMariaDb: true);

    // The rules of MySQL and MariaDB with a server's version, made on first use: a project names few versions.
    private static readonly ConcurrentDictionary<SqlDialectChoice, SqlDialectRules> ByServerVersion = new();

    private readonly QuoteReader?[] _readers = new QuoteReader?[TableSize];

    // Every character that can start something other than plain text: a comment in any dialect, a parameter, or a
    // quoted region in this one.
    private readonly char[] _starters;

    private SqlDialectRules(params (char Opener, QuoteReader Reader)[] readers)
        : this(AtSign, readers) { }

    private SqlDialectRules(char parameterPrefix, (char Opener, QuoteReader Reader)[] readers)
    {
        ParameterPrefix = parameterPrefix;
        _starters = new char[CommentStarters.Length + 1 + readers.Length];
        CommentStarters.CopyTo(0, _starters, 0, CommentStarters.Length);
        _starters[CommentStarters.Length] = parameterPrefix;
        for (var index = 0; index < readers.Length; index++)
        {
            var (opener, reader) = readers[index];
            _readers[opener] = reader;
            _starters[CommentStarters.Length + 1 + index] = opener;
        }
    }

    // The rules of source with the server's version that a versioned comment is read by.
    private SqlDialectRules(SqlDialectRules source, int serverVersion)
    {
        ParameterPrefix = source.ParameterPrefix;
        _readers = source._readers;
        _starters = source._starters;
        Dialect = source.Dialect;
        DeclaredVariables = source.DeclaredVariables;
        NestedComments = source.NestedComments;
        DashNeedsWhitespace = source.DashNeedsWhitespace;
        HashComments = source.HashComments;
        LineHints = source.LineHints;
        MariaDbHints = source.MariaDbHints;
        CarriageReturnEndsLine = source.CarriageReturnEndsLine;
        StringContinuation = source.StringContinuation;
        NamesHoldAtAndHash = source.NamesHoldAtAndHash;
        ServerVersion = serverVersion;
    }

    /// <summary>The rules of the default dialect, used when no dialect is set.</summary>
    public static SqlDialectRules Ansi { get; } =
        new(('\'', EscapeString), ('"', Doubled), ('`', Doubled), ('$', Dollar)) { NestedComments = true };

    public static SqlDialectRules SqlServer { get; } =
        new(('\'', Doubled), ('"', Doubled), ('[', EscapedBracket))
        {
            Dialect = SqlDialect.SqlServer,
            NestedComments = true,
            DeclaredVariables = true,
            NamesHoldAtAndHash = true,
        };

    public static SqlDialectRules PostgreSql { get; } =
        new(('\'', EscapeString), ('"', Doubled), ('$', Dollar))
        {
            Dialect = SqlDialect.PostgreSql,
            NestedComments = true,
            StringContinuation = SqlStringContinuation.AcrossLineComments,
        };

    public static SqlDialectRules CockroachDb { get; } =
        new(('\'', BytesEscapeString), ('"', Doubled), ('$', Dollar))
        {
            Dialect = SqlDialect.CockroachDb,
            NestedComments = true,
            StringContinuation = SqlStringContinuation.AcrossWhitespace,
            CarriageReturnEndsLine = false,
        };

    /// <summary>MySQL with no options.  <see cref="For" /> gives the rules of a set of options.</summary>
    public static SqlDialectRules MySql => MySqlByOptions[(int)SqlDialectOptions.None];

    /// <summary>MariaDB with no options.  <see cref="For" /> gives the rules of a set of options.</summary>
    public static SqlDialectRules MariaDb => MariaDbByOptions[(int)SqlDialectOptions.None];

    public static SqlDialectRules Sqlite { get; } =
        new(('\'', Doubled), ('"', Doubled), ('`', Doubled), ('[', Bracket))
        {
            Dialect = SqlDialect.Sqlite,
            CarriageReturnEndsLine = false,
        };

    public static SqlDialectRules Oracle { get; } =
        new(('\'', QuoteOperator), ('"', Doubled)) { Dialect = SqlDialect.Oracle, LineHints = true };

    /// <summary>The dialect these rules are for.</summary>
    public SqlDialect Dialect { get; private init; }

    /// <summary>The character that starts a parameter, as in <c>@id</c>.</summary>
    public char ParameterPrefix { get; }

    /// <summary>
    /// Whether a name that a query declares with <c>DECLARE</c> is a local variable and not a parameter, as in
    /// T-SQL.  <see cref="SqlDeclaredVariables" /> finds the names.
    /// </summary>
    public bool DeclaredVariables { get; private init; }

    /// <summary>Whether a <c>/*</c> inside a block comment opens a comment that needs its own <c>*/</c>.</summary>
    public bool NestedComments { get; private init; }

    /// <summary>
    /// Whether <c>--</c> starts a comment only when whitespace, a control character or the end of the text follows
    /// it, as in MySQL, where <c>5--3</c> is arithmetic.
    /// </summary>
    public bool DashNeedsWhitespace { get; private init; }

    /// <summary>Whether <c>#</c> starts a comment that runs to the end of its line.</summary>
    public bool HashComments { get; private init; }

    /// <summary>Whether <c>--+</c> starts a hint that runs to the end of its line, as in Oracle.</summary>
    public bool LineHints { get; private init; }

    /// <summary>Whether a block comment that starts <c>/*M!</c> is a hint, as in MariaDB.</summary>
    public bool MariaDbHints { get; private init; }

    /// <summary>
    /// Whether a carriage return with no line feed after it ends a line.  MySQL, MariaDB, SQLite and CockroachDB end a
    /// line only at a line feed, so to them a <c>--</c> comment runs past a carriage return alone.  A carriage return
    /// and a line feed end a line at the carriage return in every dialect.
    /// </summary>
    public bool CarriageReturnEndsLine { get; private init; } = true;

    /// <summary>
    /// The version of the server, as a versioned comment writes it: <c>80036</c> for 8.0.36, <c>101106</c> for
    /// 10.11.6.  Zero when it is not known, and under every dialect but MySQL and MariaDB.
    /// </summary>
    public int ServerVersion { get; }

    /// <summary>
    /// Whether a string goes on after a gap with a line break, and what the gap may hold.  It matters after a
    /// string whose reader names a continuation: the part after the gap is then read by that reader.
    /// </summary>
    public SqlStringContinuation StringContinuation { get; private init; }

    /// <summary>
    /// The rules of <paramref name="choice" />.  One shared instance for each choice.  Options that the dialect
    /// does not have are ignored, and so is a server's version: a name never gives them.
    /// </summary>
    public static SqlDialectRules For(SqlDialectChoice choice) =>
        choice.ServerVersion != 0 && choice.Dialect is SqlDialect.MySql or SqlDialect.MariaDb
            ? ByServerVersion.GetOrAdd(
                choice,
                static key => new SqlDialectRules(For(key with { ServerVersion = 0 }), key.ServerVersion)
            )
            : WithoutServerVersion(choice);

    private static SqlDialectRules WithoutServerVersion(SqlDialectChoice choice) =>
        choice.Dialect switch
        {
            SqlDialect.Ansi => Ansi,
            SqlDialect.SqlServer => SqlServer,
            SqlDialect.PostgreSql => PostgreSql,
            SqlDialect.MySql => MySqlByOptions[(int)choice.Options & (OptionSets - 1)],
            SqlDialect.MariaDb => MariaDbByOptions[(int)choice.Options & (OptionSets - 1)],
            SqlDialect.Sqlite => Sqlite,
            SqlDialect.Oracle => Oracle,
            SqlDialect.CockroachDb => CockroachDb,
            _ => throw UnexpectedEnumValueException.Of(choice.Dialect),
        };

    // MySQL and MariaDB read "..." as a string and take backslash escapes in both kinds of quote.  ANSI_QUOTES makes
    // "..." an identifier, which has none; NO_BACKSLASH_ESCAPES takes them from both.  MySQL reads dollar quotes,
    // and MariaDB has its own hint.
    private static SqlDialectRules[] CreateMySqlFamily(bool isMariaDb)
    {
        var family = new SqlDialectRules[OptionSets];
        for (var index = 0; index < OptionSets; index++)
        {
            var options = (SqlDialectOptions)index;
            var noBackslash = (options & SqlDialectOptions.NoBackslashEscapes) != 0;
            var ansiQuotes = (options & SqlDialectOptions.AnsiQuotes) != 0;
            var singleQuote = noBackslash ? Doubled : Backslash;
            var doubleQuote = noBackslash || ansiQuotes ? Doubled : Backslash;
            family[index] = isMariaDb
                ? new SqlDialectRules(('\'', singleQuote), ('"', doubleQuote), ('`', Doubled))
                {
                    Dialect = SqlDialect.MariaDb,
                    DashNeedsWhitespace = true,
                    HashComments = true,
                    MariaDbHints = true,
                    CarriageReturnEndsLine = false,
                }
                : new SqlDialectRules(('\'', singleQuote), ('"', doubleQuote), ('`', Doubled), ('$', Dollar))
                {
                    Dialect = SqlDialect.MySql,
                    DashNeedsWhitespace = true,
                    HashComments = true,
                    CarriageReturnEndsLine = false,
                };
        }

        return family;
    }

    // Whether a name may hold "@" and "#", and of the characters above U+007F only letters, digits, combining marks,
    // letter numbers, format characters and connectors, as T-SQL reads one.  Otherwise every character above U+007F
    // is read as a letter, as PostgreSQL and MySQL do.  A parameter's name is read by the same rule, as T-SQL reads a
    // variable's; otherwise it takes letters, digits and "_".
    private bool NamesHoldAtAndHash { get; init; }

    /// <summary>
    /// True for a character that can be part of a name in this dialect: as
    /// <see cref="QuoteReader.IsNameCharacterOfSqlServer" /> reads it under SQL Server, and as
    /// <see cref="QuoteReader.IsNameCharacterOfPostgreSql" /> does under every other dialect.
    /// </summary>
    public bool IsNameCharacter(char value) =>
        NamesHoldAtAndHash
            ? QuoteReader.IsNameCharacterOfSqlServer(value)
            : QuoteReader.IsNameCharacterOfPostgreSql(value);

    /// <summary>
    /// True for white space, and for the zero-width space where this dialect's names do not take it: SQL Server reads
    /// it as a space.
    /// </summary>
    public bool IsSpace(char value) =>
        char.IsWhiteSpace(value) || (value == QuoteReader.ZeroWidthSpace && !IsNameCharacter(value));

    /// <summary>
    /// True for a character that can be part of a parameter's name in this dialect: as
    /// <see cref="QuoteReader.IsNameCharacterOfSqlServer" /> reads it under SQL Server, the first character too, and a
    /// letter, a digit or <c>_</c> under every other dialect.
    /// </summary>
    /// <remarks>
    /// SQL Server reads a variable's name by the tables of Unicode 3.2, and this rule by the current ones of .NET, so
    /// the two can differ: <c>@x・</c> is the parameter <c>@x</c> here and one variable to the server, and <c>@ꞔ</c> is
    /// a parameter here and refused by the server.
    /// </remarks>
    public bool IsParameterNameCharacter(char value) =>
        NamesHoldAtAndHash
            ? QuoteReader.IsNameCharacterOfSqlServer(value)
            : char.IsLetterOrDigit(value) || value == '_';

    /// <summary>
    /// The offset after the name of a parameter that starts at <paramref name="nameStart" />, the character after the
    /// prefix, read no further than <paramref name="end" />.  It is <paramref name="nameStart" /> when no name
    /// follows, and when the prefix follows: <c>@@rowcount</c> is a function.
    /// </summary>
    public int FindParameterNameEnd(string text, int nameStart, int end)
    {
        if (nameStart >= end || text[nameStart] == ParameterPrefix)
        {
            return nameStart;
        }

        var index = nameStart;
        while (index < end && IsParameterNameCharacter(text[index]))
        {
            index++;
        }

        return index;
    }

    /// <summary>
    /// Whether <paramref name="value" /> ends a line in this dialect: <c>\n</c>, and <c>\r</c> where
    /// <see cref="CarriageReturnEndsLine" />.  The test for one character, where <see cref="FindLineEnd" /> searches a
    /// range.  A <c>\r</c> before a <c>\n</c> is not one under the other dialects, but the <c>\n</c> is.
    /// </summary>
    public bool IsLineTerminator(char value) => value == '\n' || (value == '\r' && CarriageReturnEndsLine);

    /// <summary>
    /// The offset where the first line from <paramref name="start" /> ends, no further than <paramref name="end" />:
    /// at its line terminator, or at the <c>\r</c> of a <c>\r\n</c>, or <paramref name="end" /> when no line ends
    /// before it.
    /// </summary>
    public int FindLineEnd(string text, int start, int end)
    {
        if (CarriageReturnEndsLine)
        {
            return SqlText.FindLineEnd(text, start, end);
        }

        var lineFeed = text.IndexOf('\n', start, end - start);
        if (lineFeed < 0)
        {
            return end;
        }

        return lineFeed > start && text[lineFeed - 1] == '\r' ? lineFeed - 1 : lineFeed;
    }

    /// <summary>
    /// The offset of the first character from <paramref name="start" /> on that can start a comment, a parameter or a
    /// quoted region, or -1 when the rest of the text is plain.  Most of a SQL file is plain text, and this skips it in
    /// one search.
    /// </summary>
    public int FindStarter(string text, int start) => text.IndexOfAny(_starters, start);

    /// <summary>
    /// Whether the server runs the body of the versioned comment that starts at <paramref name="start" />, so that
    /// its body is SQL, in which a string or a comment does not end it.  Only MySQL and MariaDB run one: <c>/*!</c>,
    /// and under MariaDB <c>/*M!</c>.  <paramref name="bodyStart" /> is where the body starts, after the version.
    /// </summary>
    /// <remarks>
    /// As MySQL 8.4 and MariaDB 11.4 read one.  A comment without a version, or with fewer than five digits, which
    /// are then part of the body, always runs.  Under MySQL, six digits followed by white space or the comment's end
    /// are a version <c>MMmmrr</c>, and otherwise the first five are one, <c>Mmmrr</c>.  Under MariaDB, six digits
    /// are a version whatever follows them, and a <c>/*!</c> version from 50700 to 99999 never runs.  A versioned
    /// comment runs when its version is not above <see cref="ServerVersion" />, and is a comment that ends at its
    /// first <c>*/</c> when that is not known.
    /// </remarks>
    public bool RunsVersionedComment(string text, int start, out int bodyStart)
    {
        bodyStart = start;
        var index = start + 2;
        var isMariaDbs = MariaDbHints && SqlText.CharAt(text, index) == 'M';
        if (isMariaDbs)
        {
            index++;
        }

        if (Dialect is not (SqlDialect.MySql or SqlDialect.MariaDb) || SqlText.CharAt(text, index) != '!')
        {
            return false;
        }

        index++;
        var digits = 0;
        while (digits < 6 && SqlText.CharAt(text, index + digits) is >= '0' and <= '9')
        {
            digits++;
        }

        if (digits < 5)
        {
            bodyStart = index;
            return true;
        }

        var length = digits == 6 && (MariaDbHints || EndsVersion(text, index + 6)) ? 6 : 5;
        var version = 0;
        for (var digit = 0; digit < length; digit++)
        {
            version = (version * 10) + (text[index + digit] - '0');
        }

        bodyStart = index + length;
        if (MariaDbHints && !isMariaDbs && version is >= 50700 and <= 99999)
        {
            return false;
        }

        return ServerVersion != 0 && version <= ServerVersion;
    }

    // What MySQL wants after six digits for them to be a version: white space or the end of the comment.
    private static bool EndsVersion(string text, int index) =>
        index >= text.Length
        || char.IsWhiteSpace(text[index])
        || (text[index] == '*' && SqlText.CharAt(text, index + 1) == '/');

    /// <summary>
    /// The reader of the quoted region that <paramref name="opener" /> opens, or null when it opens none.
    /// </summary>
    public QuoteReader? ReaderFor(char opener) => opener < TableSize ? _readers[opener] : null;
}
