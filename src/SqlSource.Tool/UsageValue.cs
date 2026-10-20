using System;
using System.CommandLine;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool;

/// <summary>
/// One value of a command line as <see cref="UsageCheck" /> read it: an argument of the command, or the value of an
/// option.  A command checks its values by rules of its own and reports one by its position, never by its text.
/// </summary>
/// <param name="Option">The option the value belongs to, or null for an argument.</param>
/// <param name="Text">
/// The value.  It may be a connection string, so it is printed only where a rule of the run allows it: a
/// <c>--database</c> name close to a database of the plan, and a path that <see cref="NamedAsPath" /> prints.
/// </param>
/// <param name="Position">The place of the token that holds it on the command line, counted from one.</param>
internal sealed record UsageValue(Option? Option, string Text, int Position)
{
    /// <summary>
    /// Whether a message may print the path this value gave: it holds no <c>=</c> and no <c>;</c>, which a connection
    /// string is made of, so a connection string typed where a path stands is not printed.  The rule reads the token
    /// and not the full path, which may hold them in a folder that is no secret.
    /// </summary>
    public bool MayBePrintedAsPath => Text.AsSpan().IndexOfAny('=', ';') < 0;

    /// <summary>
    /// How a message names the path this value gave: the full path in quotes, or else its position.  A message that
    /// takes it starts with it.
    /// </summary>
    /// <param name="fullPath">The path the value resolves to, which a message prints beside its token.</param>
    public string NamedAsPath(string fullPath) =>
        MayBePrintedAsPath ? Quoted(fullPath) : $"The path at position {Position}";

    /// <summary>
    /// A full path as a message prints it: in quotes and on one line.  For a path that is no token of the command
    /// line, such as the working directory.
    /// </summary>
    public static string Quoted(string fullPath) => $"'{OneLine.Of(fullPath)}'";

    // A record prints its members, and Text may be a secret.
    public override string ToString() => $"UsageValue {{ Option = {Option?.Name ?? "none"}, Position = {Position} }}";
}
