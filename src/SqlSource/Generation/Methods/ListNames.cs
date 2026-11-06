using System.Globalization;
using System.Text;
using SqlSource.Parsing;
using SqlSource.Settings;

namespace SqlSource.Generation.Methods;

/// <summary>
/// Names the parameters a query's method sends for the values of a list: the list's prefix followed by each value's
/// index, from 0.
/// </summary>
internal static class ListNames
{
    // A SQL Server parameter's name holds at most 128 characters, its @ among them, and an index at most four digits,
    // since a query sends SQL Server no more than 2098 parameters.
    private const int SqlServerLongestPrefix = 127 - 4;

    /// <summary>
    /// The prefix of the names a list sends, which no other parameter of the query, and no variable it declares, can
    /// take: the list's name and an underscore, <c>ids_</c>, with another underscore for as long as another parameter
    /// or a declared variable is named as the prefix and one or more digits, <c>ids_0</c>, or an earlier list has the
    /// prefix.  On SQL Server a prefix too long for a name and its index is <c>list</c>, the list's ordinal and an
    /// underscore, with underscores added the same way.  Names are compared as <see cref="SqlParameterName.Comparer" />
    /// compares them.
    /// </summary>
    /// <param name="list">The list, a parameter of <paramref name="all" />.</param>
    /// <param name="ordinal">The list's place among the lists of <paramref name="all" />, from 0.</param>
    /// <param name="all">The query's parameters.</param>
    /// <param name="declared">
    /// The names the query declares as local variables, which are none of <paramref name="all" />, but which SQL Server
    /// refuses as the name of a parameter it is sent.
    /// </param>
    /// <param name="engine">The engine of the query's file.</param>
    public static string PrefixOf(
        SqlQueryParameter list,
        int ordinal,
        EquatableArray<SqlQueryParameter> all,
        EquatableArray<string> declared,
        DatabaseEngine engine
    )
    {
        var taken = new Taken(all, declared);

        // Each earlier list's prefix is found first, so that no two lists share one.
        var earlier = new string[ordinal];
        var found = 0;
        foreach (var parameter in all)
        {
            if (found == ordinal)
            {
                break;
            }

            if (parameter.IsList)
            {
                earlier[found] = PrefixFor(parameter, found, taken, engine, earlier, found);
                found++;
            }
        }

        return PrefixFor(list, found, taken, engine, earlier, found);
    }

    private static string PrefixFor(
        SqlQueryParameter list,
        int ordinal,
        Taken taken,
        DatabaseEngine engine,
        string[] earlier,
        int earlierCount
    )
    {
        var prefix = Free(list.Name + "_", taken, earlier, earlierCount);
        return IsTooLong(prefix, engine)
            ? Free("list" + ordinal.ToString(CultureInfo.InvariantCulture) + "_", taken, earlier, earlierCount)
            : prefix;
    }

    private static bool IsTooLong(string prefix, DatabaseEngine engine) =>
        engine switch
        {
            DatabaseEngine.PostgreSql => false,
            DatabaseEngine.SqlServer => prefix.Length > SqlServerLongestPrefix,
            _ => throw UnexpectedEnumValueException.Of(engine),
        };

    // The candidate with underscores added until no parameter or declared variable is named as it and digits, and no
    // earlier list has it.  The list's own name is shorter than its candidate, so it never matches.
    private static string Free(string candidate, Taken taken, string[] earlier, int earlierCount)
    {
        if (!IsTaken(candidate, taken, earlier, earlierCount))
        {
            return candidate;
        }

        var longer = new StringBuilder(candidate);
        do
        {
            _ = longer.Append('_');
        } while (IsTaken(longer.ToString(), taken, earlier, earlierCount));

        return longer.ToString();
    }

    private static bool IsTaken(string candidate, Taken taken, string[] earlier, int earlierCount)
    {
        for (var index = 0; index < earlierCount; index++)
        {
            if (SqlParameterName.Comparer.Equals(earlier[index], candidate))
            {
                return true;
            }
        }

        for (var index = 0; index < taken.All.Count; index++)
        {
            if (IsPrefixAndDigits(taken.All[index].Name, candidate))
            {
                return true;
            }
        }

        for (var index = 0; index < taken.Declared.Count; index++)
        {
            if (IsPrefixAndDigits(taken.Declared[index], candidate))
            {
                return true;
            }
        }

        return false;
    }

    // Whether the name is the prefix followed by one or more digits.
    private static bool IsPrefixAndDigits(string name, string prefix)
    {
        if (name.Length <= prefix.Length || !name.StartsWith(prefix, SqlParameterName.Comparison))
        {
            return false;
        }

        for (var index = prefix.Length; index < name.Length; index++)
        {
            if (name[index] is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }

    // The names of the query that a list's values may not be sent by: its parameters and the variables it declares.
    private readonly record struct Taken(EquatableArray<SqlQueryParameter> All, EquatableArray<string> Declared);
}
