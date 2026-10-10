using System;
using System.Diagnostics.CodeAnalysis;
using SqlSource.Settings;

namespace SqlSource.Tool.Describing;

/// <summary>
/// One value of <c>--connection</c>: the name of a database, <c>=</c>, and its connection.
/// </summary>
/// <param name="Name">The database.</param>
/// <param name="Value">The connection.  It may hold <c>=</c> itself, and is never printed.</param>
internal sealed record ConnectionArgument(string Name, string Value)
{
    /// <summary>
    /// Reads <c>name=value</c>.  The name is the text before the first <c>=</c> and must be a database name by the
    /// rule of the <c>-- database:</c> marker; the value is the rest and must not be empty.
    /// </summary>
    /// <remarks>
    /// The option always has a name: a connection string holds <c>=</c> itself and a shell removes quotes, so the
    /// tool could not tell a name from the start of a value by looking.
    /// </remarks>
    public static bool TryParse(string text, [NotNullWhen(true)] out ConnectionArgument? argument)
    {
        argument = null;
        var separator = text.IndexOf('=', StringComparison.Ordinal);
        if (separator <= 0 || separator == text.Length - 1 || !SettingValue.IsDatabaseName(text.AsSpan(0, separator)))
        {
            return false;
        }

        argument = new ConnectionArgument(text[..separator], text[(separator + 1)..]);
        return true;
    }

    /// <summary>The argument without its value, which is a secret.</summary>
    public override string ToString() => $"ConnectionArgument {{ Name = {Name} }}";
}
