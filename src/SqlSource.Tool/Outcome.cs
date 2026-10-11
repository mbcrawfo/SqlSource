using System.Diagnostics.CodeAnalysis;

namespace SqlSource.Tool;

/// <summary>
/// A value, or why there is none.  Exactly one of the two is set.
/// </summary>
/// <remarks>
/// What a method returns that another file calls and that gives one value or one reason.  Such a method reports
/// nothing: its caller does.  <c>src/SqlSource.Tool/AGENTS.md</c> has the rule, and what is no <c>Outcome</c>.
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

    public TValue? Value { get; }

    public TFailure? Failure { get; }

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Failure))]
    public bool Succeeded { get; }

    public static Outcome<TValue, TFailure> Of(TValue value) => new(value, default, succeeded: true);

    public static Outcome<TValue, TFailure> Failed(TFailure failure) => new(default, failure, succeeded: false);
}
