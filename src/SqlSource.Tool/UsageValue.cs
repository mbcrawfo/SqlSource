using System.CommandLine;

namespace SqlSource.Tool;

/// <summary>
/// One value of a command line as <see cref="UsageCheck" /> read it: an argument of the command, or the value of an
/// option.  A command checks its values by rules of its own and reports one by its position, never by its text.
/// </summary>
/// <param name="Option">The option the value belongs to, or null for an argument.</param>
/// <param name="Text">The value.  It may be a connection string, and is never printed.</param>
/// <param name="Position">The place of the token that holds it on the command line, counted from one.</param>
internal sealed record UsageValue(Option? Option, string Text, int Position)
{
    // A record prints its members, and Text may be a secret.
    public override string ToString() => $"UsageValue {{ Option = {Option?.Name ?? "none"}, Position = {Position} }}";
}
