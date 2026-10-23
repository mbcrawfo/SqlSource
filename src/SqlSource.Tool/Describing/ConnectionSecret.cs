using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace SqlSource.Tool.Describing;

/// <summary>
/// What of one database's connection is a secret: its whole value in each form it may take, and the parts of it that
/// its describer said are secret, its passwords.  Every redaction of a run that holds the value is for this.
/// </summary>
/// <remarks>
/// <para>
/// The whole value is found wherever it stands, and so is each of its other whole forms: the value without the white
/// space at its ends, which a driver may trim, and a rewrite of it that a describer gave, which a driver may quote
/// in a message instead of the text it was given.  A form is as much the value as the value is, so it is held to
/// the rules of the value, here and in <see cref="EntrySecretCheck" />.  A part is found only where it stands as a
/// token (<see cref="Redaction.HoldsToken" />), so that a password <c>test</c> leaves <c>latest_orders</c> alone.  A
/// password that equals a whole word still finds that word.
/// </para>
/// <para>
/// Some parts are dropped.  One shorter than <see cref="MinimumPartLength" /> would match too much ordinary text.
/// One that equals a word the run writes of the database itself, its logical name or the engine's name, is printed
/// whatever the describer does, as <see cref="EntrySecretCheck" /> says of the value, so removing it hides nothing:
/// the password <c>postgres</c> would otherwise print <c>query: GetUser, database app, *** 16.4</c>.
/// </para>
/// <para>
/// It is a class and not a record, since a record prints its members.
/// </para>
/// </remarks>
/// <param name="value">The connection's value.</param>
/// <param name="parts">
/// The secret parts the describer gave.  A part that is null, empty or white space, shorter than
/// <see cref="MinimumPartLength" />, equal to the whole value or to one of <paramref name="runWords" />, and a part
/// given twice are dropped.
/// </param>
/// <param name="runWords">
/// The words the run writes of the database itself: its logical name and the engine's canonical name.  They are
/// compared ordinally.
/// </param>
/// <param name="forms">
/// The other whole forms of the value that the describer gave, such as the value as its driver writes it again.  A
/// form that is null, empty or white space, or equal to the value or to its trimmed text, and a form given twice are
/// dropped.
/// </param>
internal sealed class ConnectionSecret(
    string value,
    IEnumerable<string?>? parts = null,
    IEnumerable<string>? runWords = null,
    IEnumerable<string?>? forms = null
)
{
    /// <summary>The length below which a part is dropped.</summary>
    public const int MinimumPartLength = 4;

    /// <summary>The connection's value.</summary>
    public string Value { get; } = value;

    /// <summary>
    /// The whole forms of the value that are kept: the value itself, its trimmed text and each form, longest first so
    /// that a form that holds another is replaced before the other is.
    /// </summary>
    public ImmutableArray<string> Wholes { get; } = WholesOf(value, forms);

    /// <summary>
    /// The parts that are kept, longest first, so that a part that holds another is replaced before the other is.
    /// </summary>
    public ImmutableArray<string> Parts { get; } = PartsOf(WholesOf(value, forms), parts, runWords);

    /// <summary>Whether the text holds a whole form anywhere, or a part as a token.</summary>
    public bool IsIn(string text) =>
        Wholes.Any(whole => text.Contains(whole, StringComparison.Ordinal))
        || Parts.Any(part => Redaction.HoldsToken(text, part));

    /// <summary>The secret without its value and its parts: it is a secret.</summary>
    public override string ToString() => nameof(ConnectionSecret);

    private static ImmutableArray<string> WholesOf(string value, IEnumerable<string?>? forms) =>
        [
            .. (forms ?? [])
                .Prepend(value.Trim())
                .Prepend(value)
                .OfType<string>()
                .Where(static whole => !string.IsNullOrWhiteSpace(whole))
                .Distinct(StringComparer.Ordinal)
                .OrderByDescending(static whole => whole.Length),
        ];

    private static ImmutableArray<string> PartsOf(
        ImmutableArray<string> wholes,
        IEnumerable<string?>? parts,
        IEnumerable<string>? runWords
    )
    {
        var dropped = new HashSet<string>(runWords ?? [], StringComparer.Ordinal);
        dropped.UnionWith(wholes);
        return
        [
            .. (parts ?? [])
                .OfType<string>()
                .Where(part =>
                    part.Length >= MinimumPartLength && !string.IsNullOrWhiteSpace(part) && !dropped.Contains(part)
                )
                .Distinct(StringComparer.Ordinal)
                .OrderByDescending(static part => part.Length),
        ];
    }
}
