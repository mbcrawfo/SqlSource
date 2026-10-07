namespace SqlSource.Parsing.Quoting;

/// <summary>
/// A <c>'...'</c> string that takes backslash escapes only after a prefix, as in PostgreSQL's <c>E'it\'s'</c>.  The
/// prefix stays in the text before the region.
/// </summary>
/// <param name="prefixes">
/// The characters that are a prefix, each in the case it is accepted in: <c>Ee</c> for PostgreSQL.
/// </param>
internal sealed class EscapeStringReader(string prefixes) : QuoteReader
{
    // A part that continues an escape string takes backslash escapes with no prefix of its own.
    private static readonly QuoteReader Continued = new BackslashQuoteReader();

    public override int FindEnd(string text, int start) =>
        HasPrefix(text, start) ? FindBackslashEnd(text, start) : FindDoubledEnd(text, start);

    public override QuoteReader? FindContinuation(string text, int start) => HasPrefix(text, start) ? Continued : null;

    // A prefix is one character that does not end an identifier: the E of typeE'a' is not one.
    private bool HasPrefix(string text, int quote) =>
        quote > 0 && prefixes.IndexOf(text[quote - 1]) >= 0 && (quote < 2 || !IsIdentifierCharacter(text[quote - 2]));
}
