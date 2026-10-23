using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// The passwords of a PostgreSQL password file, and the files that Npgsql would read one from, so that the run can
/// remove a password that is in no connection string.
/// </summary>
/// <remarks>
/// Npgsql 10.0.3 (<c>PgPassFile</c>) reads a file as the tool does here: it ends a line at <c>\r</c>, <c>\n</c> or
/// <c>\r\n</c>, as a <see cref="TextReader" /> does, trims each line, skips an empty one and one that starts with
/// <c>#</c>, and splits at a colon that no backslash escapes.  Only the entry that matches the
/// connection is used, but every entry's password is given: the match depends on the host, port, database and user
/// as the driver resolves them, and a password too many removes a word that nothing else prints.
/// </remarks>
internal static class PasswordFile
{
    private const int Fields = 5;
    private const int PasswordField = 4;
    private const int PortField = 1;
    private const string Wildcard = "*";

    /// <summary>
    /// The password of each well-formed entry of a file's text.  A line that does not have five fields, or a port
    /// that is neither <c>*</c> nor a number, is skipped, since the driver could not use such a line; a password that
    /// is <c>*</c> or empty is none.
    /// </summary>
    public static IReadOnlyList<string> Passwords(string text)
    {
        var passwords = new List<string>();
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } raw)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            if (FieldsOf(line) is not { } fields)
            {
                continue;
            }

            if (fields[PortField] is { } port && !int.TryParse(port, out _))
            {
                continue;
            }

            if (fields[PasswordField] is { Length: > 0 } password)
            {
                passwords.Add(password);
            }
        }

        return passwords;
    }

    /// <summary>
    /// The files that Npgsql would read a password from: the connection string's <c>Passfile</c>, else
    /// <c>PGPASSFILE</c>, else the default one, <c>.pgpass</c> in <c>HOME</c> or <c>postgresql/pgpass.conf</c> in
    /// <c>APPDATA</c>.  The operating system decides which default the driver reads, and the tool gives each that
    /// its variable names: the other is only a file more to read.
    /// </summary>
    /// <param name="passfile">The connection string's <c>Passfile</c>, or null.</param>
    /// <param name="getEnvironmentVariable">The host's environment.</param>
    public static IReadOnlyList<string> PathsOf(string? passfile, Func<string, string?> getEnvironmentVariable)
    {
        if (passfile is { Length: > 0 })
        {
            return [passfile];
        }

        if (getEnvironmentVariable("PGPASSFILE") is { Length: > 0 } fromEnvironment)
        {
            return [fromEnvironment];
        }

        var paths = new List<string>();
        if (getEnvironmentVariable("HOME") is { Length: > 0 } home)
        {
            paths.Add(Path.Combine(home, ".pgpass"));
        }

        if (getEnvironmentVariable("APPDATA") is { Length: > 0 } appData)
        {
            paths.Add(Path.Combine(appData, "postgresql", "pgpass.conf"));
        }

        return paths;
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
                // The backslash stands for the colon or backslash it escapes, and is kept before anything else.
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
}
