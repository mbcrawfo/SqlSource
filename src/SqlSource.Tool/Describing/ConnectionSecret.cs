using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace SqlSource.Tool.Describing;

/// <summary>
/// What of one database's connection is a secret: its whole value, and the parts of it that its describer said are
/// secret, its passwords.  Every redaction and every search of a run that holds the value is for this.
/// </summary>
/// <remarks>
/// <para>
/// The whole value is found wherever it stands.  A part is found only where it stands as a token
/// (<see cref="Redaction.HoldsToken" />), so that a password <c>test</c> leaves <c>latest_orders</c> alone.  A
/// password that equals a whole word still finds that word.
/// </para>
/// <para>
/// It is a class and not a record, since a record prints its members.
/// </para>
/// </remarks>
/// <param name="value">The connection's value.</param>
/// <param name="parts">
/// The secret parts the describer gave.  A part that is null, empty or white space, the whole value, and a part
/// given twice are dropped.
/// </param>
internal sealed class ConnectionSecret(string value, IEnumerable<string?>? parts = null)
{
    /// <summary>The connection's value.</summary>
    public string Value { get; } = value;

    /// <summary>
    /// The parts that are kept, longest first, so that a part that holds another is replaced before the other is.
    /// </summary>
    public ImmutableArray<string> Parts { get; } = PartsOf(value, parts);

    /// <summary>Whether the text holds the whole value anywhere, or a part as a token.</summary>
    public bool IsIn(string text) =>
        (Value.Length > 0 && text.Contains(Value, StringComparison.Ordinal))
        || Parts.Any(part => Redaction.HoldsToken(text, part));

    /// <summary>The secret without its value and its parts: it is a secret.</summary>
    public override string ToString() => nameof(ConnectionSecret);

    private static ImmutableArray<string> PartsOf(string value, IEnumerable<string?>? parts) =>
        [
            .. (parts ?? [])
                .OfType<string>()
                .Where(part =>
                    !string.IsNullOrWhiteSpace(part) && !string.Equals(part, value, StringComparison.Ordinal)
                )
                .Distinct(StringComparer.Ordinal)
                .OrderByDescending(static part => part.Length),
        ];
}
