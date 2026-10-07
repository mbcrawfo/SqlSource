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

    private static readonly QuoteReader Doubled = new DoubledQuoteReader();

    private static readonly QuoteReader Backslash = new BackslashQuoteReader();

    private static readonly QuoteReader EscapeString = new EscapeStringReader(continues: false);

    private static readonly QuoteReader ContinuedEscapeString = new EscapeStringReader(continues: true);

    private static readonly QuoteReader QuoteOperator = new QuoteOperatorReader();

    private static readonly QuoteReader Bracket = new BracketReader(doubledCloserEscapes: false);

    private static readonly QuoteReader EscapedBracket = new BracketReader(doubledCloserEscapes: true);

    private static readonly QuoteReader Dollar = new DollarQuoteReader();

    // The rules of MySQL and of MariaDB under each set of options, at the index that the options have as a number.
    private static readonly SqlDialectRules[] MySqlByOptions = CreateMySqlFamily(isMariaDb: false);

    private static readonly SqlDialectRules[] MariaDbByOptions = CreateMySqlFamily(isMariaDb: true);

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

    /// <summary>MySQL with no options.  <see cref="For" /> gives the rules of a set of options.</summary>
    public static SqlDialectRules MySql => MySqlByOptions[(int)SqlDialectOptions.None];

    /// <summary>MariaDB with no options.  <see cref="For" /> gives the rules of a set of options.</summary>
    public static SqlDialectRules MariaDb => MariaDbByOptions[(int)SqlDialectOptions.None];

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

    /// <summary>
    /// The rules of <paramref name="choice" />.  One shared instance for each choice.  Options that the dialect
    /// does not have are ignored: a name never gives them.
    /// </summary>
    public static SqlDialectRules For(SqlDialectChoice choice) =>
        choice.Dialect switch
        {
            SqlDialect.Ansi => Ansi,
            SqlDialect.SqlServer => SqlServer,
            SqlDialect.PostgreSql => PostgreSql,
            SqlDialect.MySql => MySqlByOptions[(int)choice.Options & (OptionSets - 1)],
            SqlDialect.MariaDb => MariaDbByOptions[(int)choice.Options & (OptionSets - 1)],
            SqlDialect.Sqlite => Sqlite,
            SqlDialect.Oracle => Oracle,
            _ => Ansi,
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
                    DashNeedsWhitespace = true,
                    HashComments = true,
                    MariaDbHints = true,
                }
                : new SqlDialectRules(('\'', singleQuote), ('"', doubleQuote), ('`', Doubled), ('$', Dollar))
                {
                    DashNeedsWhitespace = true,
                    HashComments = true,
                };
        }

        return family;
    }

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
