using SqlSource.Parsing.Quoting;

namespace SqlSource.Parsing;

/// <summary>
/// How one dialect reads comments and quoted regions.  The lexer reads by the rules it is given and names no dialect.
/// </summary>
/// <remarks>
/// Every dialect-sensitive choice is here or in a <see cref="QuoteReader" />.  The static properties are the whole
/// table: a value where dialects differ by a setting, a reader where they differ by how a quoted region ends.
/// Changing a cell changes the SQL users get.
/// </remarks>
internal sealed class SqlDialectRules
{
    private const int TableSize = 128;

    // The first characters of --, # and /*.
    private const string CommentStarters = "-#/";

    private static readonly QuoteReader Doubled = new DoubledQuoteReader();

    private static readonly QuoteReader Backslash = new BackslashQuoteReader();

    private static readonly QuoteReader EscapeString = new EscapeStringReader(continues: false);

    private static readonly QuoteReader ContinuedEscapeString = new EscapeStringReader(continues: true);

    private static readonly QuoteReader QuoteOperator = new QuoteOperatorReader();

    private static readonly QuoteReader Bracket = new BracketReader(doubledCloserEscapes: false);

    private static readonly QuoteReader EscapedBracket = new BracketReader(doubledCloserEscapes: true);

    private static readonly QuoteReader Dollar = new DollarQuoteReader();

    private readonly QuoteReader?[] _readers = new QuoteReader?[TableSize];

    // Every character that can start something other than plain text: a comment in any dialect, or a quoted region
    // in this one.
    private readonly char[] _starters;

    private SqlDialectRules(params (char Opener, QuoteReader Reader)[] readers)
    {
        _starters = new char[CommentStarters.Length + readers.Length];
        CommentStarters.CopyTo(0, _starters, 0, CommentStarters.Length);
        for (var index = 0; index < readers.Length; index++)
        {
            var (opener, reader) = readers[index];
            _readers[opener] = reader;
            _starters[CommentStarters.Length + index] = opener;
        }
    }

    /// <summary>Today's rules for every database, unchanged by the dialect setting.</summary>
    public static SqlDialectRules Ansi { get; } =
        new(('\'', EscapeString), ('"', Doubled), ('`', Doubled), ('$', Dollar)) { NestedComments = true };

    public static SqlDialectRules SqlServer { get; } =
        new(('\'', Doubled), ('"', Doubled), ('[', EscapedBracket)) { NestedComments = true };

    public static SqlDialectRules PostgreSql { get; } =
        new(('\'', ContinuedEscapeString), ('"', Doubled), ('$', Dollar)) { NestedComments = true };

    public static SqlDialectRules MySql { get; } =
        new(('\'', Backslash), ('"', Backslash), ('`', Doubled), ('$', Dollar))
        {
            DashNeedsWhitespace = true,
            HashComments = true,
        };

    public static SqlDialectRules MariaDb { get; } =
        new(('\'', Backslash), ('"', Backslash), ('`', Doubled))
        {
            DashNeedsWhitespace = true,
            HashComments = true,
            MariaDbHints = true,
        };

    public static SqlDialectRules Sqlite { get; } =
        new(('\'', Doubled), ('"', Doubled), ('`', Doubled), ('[', Bracket));

    public static SqlDialectRules Oracle { get; } = new(('\'', QuoteOperator), ('"', Doubled)) { LineHints = true };

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

    /// <summary>The rules of <paramref name="dialect" />.  One shared instance for each dialect.</summary>
    public static SqlDialectRules For(SqlDialect dialect) =>
        dialect switch
        {
            SqlDialect.Ansi => Ansi,
            SqlDialect.SqlServer => SqlServer,
            SqlDialect.PostgreSql => PostgreSql,
            SqlDialect.MySql => MySql,
            SqlDialect.MariaDb => MariaDb,
            SqlDialect.Sqlite => Sqlite,
            SqlDialect.Oracle => Oracle,
            _ => Ansi,
        };

    /// <summary>
    /// The offset of the first character from <paramref name="start" /> on that can start a comment or a quoted
    /// region, or -1 when the rest of the text is plain.  Most of a SQL file is plain text, and this skips it in one
    /// search.
    /// </summary>
    public int FindStarter(string text, int start) => text.IndexOfAny(_starters, start);

    /// <summary>
    /// The reader of the quoted region that <paramref name="opener" /> opens, or null when it opens none.
    /// </summary>
    public QuoteReader? ReaderFor(char opener) => opener < TableSize ? _readers[opener] : null;
}
