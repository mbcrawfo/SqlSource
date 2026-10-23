using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SqlSource.Parsing;
using SqlSource.Snapshot;

namespace SqlSource.Tool.Describing.SqlServer;

/// <summary>The SQL with each use of a reused parameter renamed to a copy of its own.</summary>
/// <param name="Sql">The SQL that is sent: the user's, with only the uses of the reused parameters renamed.</param>
/// <param name="Reused">The reused parameters, in the order of the query's list.</param>
/// <param name="ParameterOfCopy">
/// Each copy's name, without its <c>@</c>, to its parameter's name as the list spells it.  It ignores case, as the
/// server does.
/// </param>
internal sealed record CopiedSql(
    string Sql,
    IReadOnlyList<ReusedParameter> Reused,
    IReadOnlyDictionary<string, string> ParameterOfCopy
);

/// <summary>A parameter that the SQL uses more than once, and the names of its copies.</summary>
/// <param name="Name">The parameter's name as the list spells it.</param>
/// <param name="Copies">The copies' names, without their <c>@</c>, in the order the SQL uses them.</param>
internal sealed record ReusedParameter(string Name, IReadOnlyList<string> Copies);

/// <summary>
/// What the server said of one copy.  It is no record of a call, so its name does not end in <c>Answer</c>, which
/// would hold it to the rule of a capture.
/// </summary>
/// <param name="Type">The type <c>parameters</c> gave the copy, or null when it gave none.</param>
/// <param name="Ignored">
/// True when the copy binds declared <c>xml</c> and declared <c>int</c>: its context types nothing.
/// </param>
internal sealed record CopyFinding(SqlServerType? Type, bool Ignored);

/// <summary>Why the copies of a parameter give it no type.</summary>
/// <param name="First">The first type of the copies left, or null when no type is left.</param>
/// <param name="Second">The first type of the copies left that differs from <paramref name="First" />, or null.</param>
/// <remarks>
/// Both null: no copy is left, or one left is untyped, <c>SQLSRC226</c>.  Both set: the copies left have two types,
/// <c>SQLSRC262</c>.
/// </remarks>
internal sealed record CopiesFailure(SqlServerType? First, SqlServerType? Second);

/// <summary>A parameter that one of the lexer's list and the server's lacks.</summary>
/// <param name="Name">The parameter's name without its <c>@</c>: as the list spells it, or as the server does.</param>
/// <param name="ReadBySqlSource">
/// True when the list holds it and the server did not report it; false when the server reported it and the list
/// lacks it.
/// </param>
internal sealed record ListDifference(string Name, bool ReadBySqlSource);

/// <summary>
/// The copies of a reused parameter: the rename of each of its uses, the rule that unifies the copies' types, and
/// the comparison of the server's parameters with the query's list.
/// </summary>
/// <remarks>
/// <para>
/// <c>sp_describe_undeclared_parameters</c> refuses a parameter used twice, error 11508.  So each use of a parameter
/// that the list holds undeclared, and that the lexer finds more than once, is renamed to a copy: the parameter's
/// name, a separator of <c>_</c> and the use's number, counted from one.  The separator is as many <c>_</c> as make
/// every copy's name differ, ignoring case, from every name the text holds as a parameter or a variable, from every
/// name of the list, and from every other copy's.  A copy's name is at most <see cref="MaximumCopyLength" />
/// characters: one that would be longer has its parameter's part cut to fit, and a number whose copy then meets a
/// name already held is passed over, so the copies of such a parameter are numbered in order but not always from one
/// without a gap.  A parameter whose own name is longer than that is not cut, so that the server refuses its copies
/// as it refuses the name used once, with the same error.  Only <see cref="SqlLexemeKind.Parameter" /> lexemes are
/// renamed, so a string, a comment and a quoted identifier are left as written.
/// </para>
/// <para>
/// A parameter is declared when its marker gives a type, which goes to the server in the declarations: the server
/// takes a declared parameter any number of times.  A <c>DECLARE</c>d variable is in no list, and is left as written.
/// </para>
/// </remarks>
internal static class ParameterCopies
{
    /// <summary>
    /// The longest name of a copy, and of any parameter, without its <c>@</c>: SQL Server's identifier holds 128
    /// characters, and a variable's counts its <c>@</c>.
    /// </summary>
    public const int MaximumCopyLength = 127;

    private const char Prefix = '@';

    // Prefix as the text WithoutPrefix takes, from the one constant.
    private static readonly string Prefixes = Prefix.ToString();

    /// <summary>The SQL with every use of each reused parameter renamed to a copy.</summary>
    /// <exception cref="InvalidOperationException">The SQL does not lex: a bug of the plan.</exception>
    public static CopiedSql Rename(string sql, EquatableArray<SqlQueryParameter> parameters)
    {
        var lexemes = SqlServerSql.Lex(sql);
        var uses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var lexeme in lexemes)
        {
            if (lexeme.Kind == SqlLexemeKind.Parameter)
            {
                var name = NameOf(sql, lexeme);
                _ = taken.Add(name);
                uses[name] = uses.GetValueOrDefault(name) + 1;
            }
        }

        var reused = new List<(string Name, int Uses)>();
        for (var index = 0; index < parameters.Count; index++)
        {
            var parameter = parameters[index];
            _ = taken.Add(parameter.Name);
            if (parameter.Type is null && uses.GetValueOrDefault(parameter.Name) > 1)
            {
                reused.Add((parameter.Name, uses[parameter.Name]));
            }
        }

        if (reused.Count == 0)
        {
            return new CopiedSql(sql, [], new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }

        var separator = SeparatorFor(reused, taken);
        var made = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var copies = reused
            .Select(parameter => new ReusedParameter(parameter.Name, CopiesOf(parameter, separator, taken, made)))
            .ToList();
        return new CopiedSql(Renamed(sql, lexemes, copies), copies, ParameterOfCopy(copies));
    }

    /// <summary>The one type of every copy, or null when a copy is untyped or two types differ.</summary>
    /// <remarks>
    /// Types are compared whole, by name, facets and user type: <c>dbo.Email</c> is not the <c>nvarchar(320)</c> it
    /// stands over.
    /// </remarks>
    public static SqlServerType? Agreed(IReadOnlyList<SqlServerType?> types)
    {
        if (types.Count == 0 || types[0] is not { } first)
        {
            return null;
        }

        return types.All(first.Equals) ? first : null;
    }

    /// <summary>
    /// The type of a reused parameter from what the server said of its copies, once the ignored ones are left out:
    /// their one type, or why there is none.
    /// </summary>
    public static Outcome<SqlServerType, CopiesFailure> Unify(IReadOnlyList<CopyFinding> copies)
    {
        var left = copies.Where(static copy => !copy.Ignored).ToList();
        if (left.Count == 0 || left.Exists(static copy => copy.Type is null))
        {
            return Outcome<SqlServerType, CopiesFailure>.Failed(new CopiesFailure(null, null));
        }

        var first = left[0].Type!;
        return left.Find(copy => !first.Equals(copy.Type)) is { Type: { } second }
            ? Outcome<SqlServerType, CopiesFailure>.Failed(new CopiesFailure(first, second))
            : Outcome<SqlServerType, CopiesFailure>.Of(first);
    }

    /// <summary>
    /// The parameters that one list holds and the other lacks, compared ignoring case: those of the query's list
    /// first, in its order, then those of the server's, in its order.
    /// </summary>
    /// <param name="undeclared">The names of the query's undeclared parameters, as the list spells them.</param>
    /// <param name="reported">The names the server reported, each with its <c>@</c>.</param>
    /// <param name="parameterOfCopy">Each copy's parameter, by which a reported copy is taken back to it.</param>
    public static IReadOnlyList<ListDifference> CompareLists(
        IEnumerable<string> undeclared,
        IEnumerable<string> reported,
        IReadOnlyDictionary<string, string> parameterOfCopy
    )
    {
        var listed = undeclared.ToList();
        var fromServer = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in reported)
        {
            var bare = SqlParameterName.WithoutPrefix(name, Prefixes);
            var parameter = parameterOfCopy.TryGetValue(bare, out var original) ? original : bare;
            if (seen.Add(parameter))
            {
                fromServer.Add(parameter);
            }
        }

        var inList = new HashSet<string>(listed, StringComparer.OrdinalIgnoreCase);
        return
        [
            .. listed.Where(name => !seen.Contains(name)).Select(static name => new ListDifference(name, true)),
            .. fromServer.Where(name => !inList.Contains(name)).Select(static name => new ListDifference(name, false)),
        ];
    }

    // The fewest "_" that make every copy's name one that nothing else holds.
    private static string SeparatorFor(List<(string Name, int Uses)> reused, HashSet<string> taken)
    {
        var length = 1;
        while (!AreFree(reused, new string('_', length), taken))
        {
            length++;
        }

        return new string('_', length);
    }

    private static bool AreFree(List<(string Name, int Uses)> reused, string separator, HashSet<string> taken)
    {
        var made = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, uses) in reused)
        {
            for (var number = 1; number <= uses; number++)
            {
                var copy = CopyName(name, separator, number);
                if (taken.Contains(copy) || !made.Add(copy))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static string Renamed(string sql, EquatableArray<SqlLexeme> lexemes, List<ReusedParameter> copies)
    {
        var byName = copies.ToDictionary(static copy => copy.Name, StringComparer.OrdinalIgnoreCase);
        var next = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var renamed = new StringBuilder(sql.Length + (copies.Count * 8));
        foreach (var lexeme in lexemes)
        {
            if (lexeme.Kind == SqlLexemeKind.Parameter && byName.TryGetValue(NameOf(sql, lexeme), out var reused))
            {
                var use = next.GetValueOrDefault(reused.Name);
                next[reused.Name] = use + 1;
                _ = renamed.Append(Prefix).Append(reused.Copies[use]);
                continue;
            }

            _ = renamed.Append(sql, lexeme.Span.Start, lexeme.Span.Length);
        }

        return renamed.ToString();
    }

    private static Dictionary<string, string> ParameterOfCopy(List<ReusedParameter> copies)
    {
        var parameterOfCopy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var reused in copies)
        {
            foreach (var copy in reused.Copies)
            {
                parameterOfCopy.Add(copy, reused.Name);
            }
        }

        return parameterOfCopy;
    }

    // The copies of one parameter, numbered in order of use.  A number whose copy meets a name the text or the list
    // holds, or another copy, is passed over: that happens only when a name was cut to fit.
    private static List<string> CopiesOf(
        (string Name, int Uses) parameter,
        string separator,
        HashSet<string> taken,
        HashSet<string> made
    )
    {
        var copies = new List<string>(parameter.Uses);
        var number = 0;
        while (copies.Count < parameter.Uses)
        {
            number++;
            var copy = FittedCopyName(parameter.Name, separator, number);
            if (!taken.Contains(copy) && made.Add(copy))
            {
                copies.Add(copy);
            }
        }

        return copies;
    }

    // The copy's name, its parameter's part cut so that the whole is no longer than MaximumCopyLength.  A name that is
    // itself longer is not cut: the server refuses its copies as it refuses the name used once.
    private static string FittedCopyName(string name, string separator, int number)
    {
        var suffix = separator + number.ToString(CultureInfo.InvariantCulture);
        if (name.Length > MaximumCopyLength || name.Length + suffix.Length <= MaximumCopyLength)
        {
            return name + suffix;
        }

        return name[..Math.Max(MaximumCopyLength - suffix.Length, 0)] + suffix;
    }

    private static string CopyName(string name, string separator, int number) =>
        name + separator + number.ToString(CultureInfo.InvariantCulture);

    private static string NameOf(string sql, SqlLexeme lexeme)
    {
        var name = lexeme.ParameterName;
        return sql.Substring(name.Start, name.Length);
    }
}
