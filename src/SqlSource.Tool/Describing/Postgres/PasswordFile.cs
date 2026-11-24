using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// The passwords of a PostgreSQL password file that Npgsql may use for a connection, and the file it would read one
/// from, so that the run can remove a password that is in no connection string.
/// </summary>
/// <remarks>
/// <para>
/// Npgsql 10.0.3 (<c>PgPassFile</c>) reads a file as the tool does here: it ends a line at <c>\r</c>, <c>\n</c> or
/// <c>\r\n</c>, as a <see cref="TextReader" /> does, trims each line, skips an empty one and one that starts with
/// <c>#</c>, and splits at a colon that no backslash escapes.  It throws at a line that does not have five fields or
/// whose port is neither <c>*</c> nor a number, so no entry after one is ever used.
/// </para>
/// <para>
/// It uses the first entry that matches the host, the port, the database and the user of the connection, compared
/// ordinally, where <c>*</c> matches anything (<c>NpgsqlConnector.GetPassword</c>).  The tool gives the password of
/// each entry that may be that one: every entry that matches what the tool knows of the connection, up to the first
/// that surely matches.  What it cannot know matches any entry and is never sure: the user that Npgsql takes from
/// Kerberos or from the system when neither the string nor <c>PGUSER</c> names one, and the host of a string with none.
/// A string with no database matches every entry's, as Npgsql's does.  A password too many only removes a word that
/// nothing else prints.
/// </para>
/// </remarks>
internal static class PasswordFile
{
    private const int Fields = 5;
    private const int HostField = 0;
    private const int PortField = 1;
    private const int DatabaseField = 2;
    private const int UserField = 3;
    private const int PasswordField = 4;
    private const string Wildcard = "*";

    private enum Match
    {
        No,
        Maybe,
        Sure,
    }

    /// <summary>
    /// The password of each entry of a file's text that Npgsql may use for the target, in the file's order, up to the
    /// first entry that surely matches or the first line it could not read.  A password that is <c>*</c> or empty is
    /// none.
    /// </summary>
    public static IReadOnlyList<string> Passwords(string text, Target target)
    {
        var entries = Entries(text);
        var passwords = new List<string>();
        foreach (var server in target.Servers)
        {
            foreach (var entry in entries)
            {
                var match = Combined(
                    Of(server.Host, entry[HostField]),
                    Of(server.Port, entry[PortField]),
                    target.Database is null ? Match.Sure : Of(target.Database, entry[DatabaseField]),
                    Of(target.User, entry[UserField])
                );
                if (match == Match.No)
                {
                    continue;
                }

                if (entry[PasswordField] is { Length: > 0 } password && !passwords.Contains(password))
                {
                    passwords.Add(password);
                }

                if (match == Match.Sure)
                {
                    break;
                }
            }
        }

        return passwords;
    }

    /// <summary>
    /// The file that Npgsql would read a password from: the connection string's <c>Passfile</c>, else
    /// <c>PGPASSFILE</c>, else the default one of the system, <c>postgresql/pgpass.conf</c> in <c>APPDATA</c> on
    /// Windows and <c>.pgpass</c> in <c>HOME</c> elsewhere (<c>PostgresEnvironment.PassFileDefault</c>).
    /// </summary>
    /// <param name="passfile">The connection string's <c>Passfile</c>, or null.</param>
    /// <param name="getEnvironmentVariable">The host's environment.</param>
    /// <param name="windows">Whether the tool runs on Windows.</param>
    public static IReadOnlyList<string> PathsOf(
        string? passfile,
        Func<string, string?> getEnvironmentVariable,
        bool windows
    )
    {
        if (passfile is { Length: > 0 })
        {
            return [passfile];
        }

        if (getEnvironmentVariable("PGPASSFILE") is { Length: > 0 } fromEnvironment)
        {
            return [fromEnvironment];
        }

        return (windows, getEnvironmentVariable(windows ? "APPDATA" : "HOME")) switch
        {
            (_, null or "") => [],
            (true, var appData) => [Path.Combine(appData, "postgresql", "pgpass.conf")],
            (false, var home) => [Path.Combine(home, ".pgpass")],
        };
    }

    /// <summary>
    /// The servers a host of a connection string names, each with its port, as Npgsql reads them: several hosts
    /// apart at commas, each trimmed and split from a port after its last colon (<c>TrySplitHostPort</c>), and a single
    /// host split so only when it is not the path of a socket (<c>PostProcessAndValidate</c>).  A host whose port is
    /// no number is kept whole: Npgsql throws there and uses no password.  A host that is null or white space is
    /// unknown.
    /// </summary>
    public static IReadOnlyList<Server> ServersOf(string? host, int port)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return [new Server(null, port)];
        }

        if (host.Contains(',', StringComparison.Ordinal))
        {
            return
            [
                .. host.Split(',')
                    .Select(static each => each.Trim())
                    .Select(each => SplitPort(each) ?? new Server(each, port))
                    .Distinct(),
            ];
        }

        // Npgsql's IsUnixSocket: a rooted path, as the system roots one, or an abstract socket's "@".
        var socket = Path.IsPathRooted(host) || host[0] == '@';
        return [(socket ? null : SplitPort(host)) ?? new Server(host, port)];
    }

    // A host with its own port, as Npgsql's TrySplitHostPort splits it, or null: a port that is no number is null
    // too, since Npgsql throws there and opens nothing.
    private static Server? SplitPort(string host)
    {
        var separator = host.LastIndexOf(':');
        if (separator < 0)
        {
            return null;
        }

        var otherColon = host[..separator].LastIndexOf(':');
        var ipv6End = host.LastIndexOf(']');
        var splits = otherColon == -1 || (separator > ipv6End && otherColon < ipv6End);
        return
            splits
            && int.TryParse(
                host.AsSpan(separator + 1),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var port
            )
            ? new Server(host[..separator], port)
            : null;
    }

    // The fields of each entry, null for one that is "*", up to the first line Npgsql could not read.
    private static List<string?[]> Entries(string text)
    {
        var entries = new List<string?[]>();
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } raw)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            if (
                FieldsOf(line) is not { } fields
                || (
                    fields[PortField] is { } port
                    && !int.TryParse(port, NumberStyles.Integer, CultureInfo.CurrentCulture, out _)
                )
            )
            {
                break;
            }

            entries.Add(fields);
        }

        return entries;
    }

    // How a value of the connection matches an entry's field: a field that is "*" surely, an unknown value maybe.
    private static Match Of(string? value, string? field) =>
        (value, field) switch
        {
            (_, null) => Match.Sure,
            (null, _) => Match.Maybe,
            _ when string.Equals(value, field, StringComparison.Ordinal) => Match.Sure,
            _ => Match.No,
        };

    // Entries holds only a field that is a number.
    private static Match Of(int? port, string? field) =>
        (port, field) switch
        {
            (_, null) => Match.Sure,
            (null, _) => Match.Maybe,
            _ when int.Parse(field, CultureInfo.CurrentCulture) == port => Match.Sure,
            _ => Match.No,
        };

    private static Match Combined(params Match[] matches)
    {
        if (matches.Contains(Match.No))
        {
            return Match.No;
        }

        return matches.Contains(Match.Maybe) ? Match.Maybe : Match.Sure;
    }

    // The fields of a line, null for a field that is "*", or null when the line does not have five.  A backslash
    // before a colon or a backslash gives that character; before anything else it is kept with the character.
    private static string?[]? FieldsOf(string line)
    {
        var fields = new List<string?>();
        var field = new StringBuilder();
        var escaped = false;
        foreach (var c in line)
        {
            if (escaped)
            {
                if (c is not (':' or '\\'))
                {
                    _ = field.Append('\\');
                }

                _ = field.Append(c);
                escaped = false;
            }
            else if (c == '\\')
            {
                escaped = true;
            }
            else if (c == ':')
            {
                fields.Add(Finish(field));
            }
            else
            {
                _ = field.Append(c);
            }
        }

        // A backslash that ends the line is kept.
        if (escaped)
        {
            _ = field.Append('\\');
        }

        fields.Add(Finish(field));
        return fields.Count == Fields ? [.. fields] : null;
    }

    private static string? Finish(StringBuilder field)
    {
        var text = field.ToString();
        _ = field.Clear();
        return text == Wildcard ? null : text;
    }

    /// <summary>A host and port that Npgsql may connect to.  A null one is one the tool does not know.</summary>
    internal sealed record Server(string? Host, int? Port);

    /// <summary>What Npgsql matches an entry against.</summary>
    /// <param name="Servers">Each host and port it may connect to.</param>
    /// <param name="Database">The connection string's database, or null for none, which matches every entry.</param>
    /// <param name="User">The user, or null when Npgsql would find it in a way the tool does not follow.</param>
    internal sealed record Target(IReadOnlyList<Server> Servers, string? Database, string? User)
    {
        /// <summary>A target of which nothing is known, which every entry may match.</summary>
        public static Target Unknown { get; } = new([new Server(null, null)], null, null);
    }
}
