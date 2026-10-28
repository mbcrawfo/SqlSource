using System.Diagnostics.CodeAnalysis;

namespace SqlSource.Tool;

/// <summary>
/// A value, or why there is none.  Exactly one of the two is set.
/// </summary>
/// <remarks>
/// <para>
/// What a method returns that another file calls and that gives one value or one reason.  Such a method reports
/// nothing: its caller does, as <c>Cli.OpenLog</c> reports what <c>RunLogFile.Open</c> did.
/// </para>
/// <para>
/// What is no <c>Outcome</c>: a private helper may be a <c>TryX</c>, and so may a method that gives a value or
/// nothing and no reason, as <c>ConnectionArgument.TryParse</c>.  A result with more than two states is a record
/// with a state, as <c>ProjectEvaluation</c>.  A step that reports several errors as it goes takes the reporter, or
/// returns its errors as data, and is no <c>Outcome</c>.
/// </para>
/// </remarks>
/// <typeparam name="TValue">What there is when it succeeded.</typeparam>
/// <typeparam name="TFailure">Why there is no value: a reason as text, an error, a describer's failure.</typeparam>
internal sealed record Outcome<TValue, TFailure>
{
    private Outcome(TValue? value, TFailure? failure, bool succeeded)
    {
        Value = value;
        Failure = failure;
        Succeeded = succeeded;
    }

    /// <summary>
    /// A success that holds <paramref name="value"/>.
    /// </summary>
    /// <remarks>
    /// How an <c>Outcome</c> of a value that must be disposed is made, in place of <see cref="Of"/>: the
    /// <c>.editorconfig</c> tells <c>CA2000</c> that a constructor takes ownership of what it is given, so the method
    /// that returns the <c>Outcome</c> carries no <c>SuppressMessage</c>, as <c>RunLogFile.Open</c> and
    /// <c>DescriberSession.OpenAsync</c> show.  <c>docs/architecture/tool.md</c> says what that costs.
    /// </remarks>
    /// <param name="value">What there is.</param>
    public Outcome(TValue value)
        : this(value, default, succeeded: true) { }

    public TValue? Value { get; }

    public TFailure? Failure { get; }

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Failure))]
    public bool Succeeded { get; }

    /// <summary>
    /// A success that holds <paramref name="value"/>, for a value that needs no disposing; one that does is made with
    /// the constructor, <see cref="Outcome{TValue, TFailure}(TValue)"/>.
    /// </summary>
    public static Outcome<TValue, TFailure> Of(TValue value) => new(value);

    public static Outcome<TValue, TFailure> Failed(TFailure failure) => new(default, failure, succeeded: false);
}
