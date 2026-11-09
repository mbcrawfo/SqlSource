namespace SqlSource.Generation.Repositories;

/// <summary>
/// The member that gives a repository's methods their connection for one database.
/// </summary>
/// <param name="Kind">How a method gets the connection from it, which decides who owns it.</param>
/// <param name="MemberName">The member's name, as a value: without the <c>@</c> of an escaped keyword.</param>
/// <param name="AccessText">
/// The member as a method of the type reaches it, without the call of a method: <c>this.X</c> for an instance member,
/// <c>X</c> for a primary-constructor parameter, and the type's name as its own code writes it, with its type
/// parameters, then <c>.X</c>, for a static member, a base type's included.  A keyword gets its <c>@</c>.
/// </param>
/// <param name="IsAsync">Whether it is a factory that returns a <c>Task</c> or a <c>ValueTask</c>.</param>
/// <param name="TakesCancellationToken">Whether it is a method that takes a <c>CancellationToken</c>.</param>
internal sealed record ConnectionSource(
    ConnectionSourceKind Kind,
    string MemberName,
    string AccessText,
    bool IsAsync,
    bool TakesCancellationToken
);
