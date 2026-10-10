using System.Collections.Generic;
using System.Linq;

namespace SqlSource.Tool.Logging;

/// <summary>
/// A value of an event of the log: what JSON can hold, and nothing else.  The set is closed, so that the writer
/// writes every value and <see cref="SessionRunLog" /> reaches every string.
/// </summary>
internal abstract record LogValue
{
    // Only the records below derive from it.
    private LogValue() { }

    /// <summary>The value <c>null</c>.</summary>
    public static LogValue Null { get; } = new Nothing();

    public static implicit operator LogValue(string? text) => text is null ? Null : new Text(text);

    public static implicit operator LogValue(bool flag) => new Flag(flag);

    public static implicit operator LogValue(long number) => new Number(number);

    /// <summary>A number, or <c>null</c> when there is none.</summary>
    public static LogValue Of(long? number) => number is { } value ? new Number(value) : Null;

    public static LogValue ListOf(IEnumerable<LogValue> items) => new Items(new EquatableArray<LogValue>([.. items]));

    public static LogValue ListOfTexts(IEnumerable<string> texts) =>
        ListOf(texts.Select(static text => (LogValue)text));

    /// <summary>Names and values, in the order given.</summary>
    public static LogValue MapOf(params LogMember[] members) =>
        new Members(new EquatableArray<LogMember>([.. members]));

    internal sealed record Nothing : LogValue;

    internal sealed record Text(string Value) : LogValue;

    internal sealed record Flag(bool Value) : LogValue;

    internal sealed record Number(long Value) : LogValue;

    // Each holds the repository's array that compares by its items, so two values that hold the same are equal.
    internal sealed record Items(EquatableArray<LogValue> Values) : LogValue;

    internal sealed record Members(EquatableArray<LogMember> Values) : LogValue;
}
