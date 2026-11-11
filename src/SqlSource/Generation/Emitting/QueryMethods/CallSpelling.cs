using System.Text;
using SqlSource.Generation.Methods;

namespace SqlSource.Generation.Emitting.QueryMethods;

/// <summary>
/// Spells what makes a query's method asynchronous or synchronous: a call of the driver, its token, an awaited
/// expression, a disposal scope and the type the method returns.  No phase file spells them itself; each asks the
/// method's <see cref="QueryMethodContext.Spelling" />, which <see cref="For" /> chooses by the method's
/// <see cref="QueryMethod.Synchrony" />.  See "Adding a kind of method" in the generator's architecture page.
/// </summary>
internal abstract class CallSpelling
{
    /// <summary>The spelling of a method of the synchrony.</summary>
    public static CallSpelling For(MethodSynchrony synchrony) =>
        synchrony switch
        {
            MethodSynchrony.Asynchronous => AsyncCallSpelling.Instance,
            MethodSynchrony.Synchronous => SyncCallSpelling.Instance,
            _ => throw UnexpectedEnumValueException.Of(synchrony),
        };

    /// <summary>
    /// Appends the start of a call of the driver on the target, up to its opening parenthesis: <c>await
    /// target.ReadAsync(</c> or <c>target.Read(</c>.  The caller follows it with the arguments, the token's
    /// <see cref="AppendToken" /> among them, and <see cref="AppendCallEnd" />.
    /// </summary>
    public abstract StringBuilder AppendCallStart(StringBuilder builder, string target, DriverCall call);

    /// <summary>
    /// Appends a call's cancellation token, after a comma when an argument comes before it, or nothing for a method
    /// that takes no token.
    /// </summary>
    public abstract StringBuilder AppendToken(StringBuilder builder, string token, bool afterArgument);

    /// <summary>
    /// Appends the <c>)</c> that closes a call's arguments, and for an awaited one what configures its await.
    /// </summary>
    public abstract StringBuilder AppendCallEnd(StringBuilder builder);

    /// <summary>
    /// Appends what comes before an awaited expression that is no call of the driver, such as a factory's: <c>await
    /// </c>, or nothing.  The caller ends the expression's call with <see cref="AppendCallEnd" />.
    /// </summary>
    public abstract StringBuilder AppendAwait(StringBuilder builder);

    /// <summary>
    /// Appends the line that declares a local that disposes the disposable when the method ends, at the indent.
    /// </summary>
    public abstract StringBuilder AppendUsing(StringBuilder builder, string indent, string scope, string disposable);

    /// <summary>
    /// Appends what comes between a method's modifiers and its name: <c> async Task&lt;resultType&gt;</c> or
    /// <c> resultType</c>, or for a method without a result <c> async Task</c> or <c> void</c>.
    /// </summary>
    public abstract StringBuilder AppendReturnType(StringBuilder builder, string? resultType);

    /// <summary>
    /// Appends the type that a member of a repository's interface returns: <c>Task&lt;resultType&gt;</c> or
    /// <c>resultType</c>, or for a member without a result <c>Task</c> or <c>void</c>.
    /// </summary>
    public abstract StringBuilder AppendInterfaceReturnType(StringBuilder builder, string? resultType);
}
