namespace SqlSource.Generation.Repositories;

/// <summary>
/// The member that gives a repository's methods their connection for one database.
/// </summary>
/// <param name="Kind">How a method gets the connection from it, which decides who owns it.</param>
/// <param name="Member">
/// What kind of member it is: a method, a connection string's included, is called, and anything else read.
/// </param>
/// <param name="MemberName">The member's name, as a value: without the <c>@</c> of an escaped keyword.</param>
/// <param name="AccessText">
/// The member as a method of the type reaches it, without the call of a method: <c>this.X</c> for an instance member,
/// <c>X</c> for a primary-constructor parameter, and the type's name as its own code writes it, with its type
/// parameters, then <c>.X</c>, for a static member, a base type's included.  A keyword gets its <c>@</c>.
/// </param>
/// <param name="IsAsync">Whether it is a factory that returns a <c>Task</c> or a <c>ValueTask</c>.</param>
/// <param name="TakesCancellationToken">Whether it is a method that takes a <c>CancellationToken</c>.</param>
/// <param name="LeadingName">
/// The name, as a value, that <paramref name="AccessText" /> starts with and that an argument or a local of the method
/// of that name would hide: the parameter's, or the type's for a static member.  Null for an instance member, reached
/// through <c>this</c>.
/// </param>
/// <param name="TypeName">
/// The repository's name, as a diagnostic gives it, which the method's exception names when the member is null.
/// </param>
internal sealed record ConnectionSource(
    ConnectionSourceKind Kind,
    CandidateMember Member,
    string MemberName,
    string AccessText,
    bool IsAsync,
    bool TakesCancellationToken,
    string? LeadingName,
    string TypeName
);
