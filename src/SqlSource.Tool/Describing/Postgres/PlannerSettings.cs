using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// The settings of the planner that a request of <c>explain</c> may carry, each as <c>name=value</c>.  The tests
/// force a join method with them; the tool itself passes none.
/// </summary>
/// <remarks>
/// The list is closed and a value is <c>on</c> or <c>off</c>, so the text of a <c>SET LOCAL</c> holds nothing but
/// what is written here.
/// </remarks>
internal static class PlannerSettings
{
    private static readonly string[] Names =
    [
        "enable_hashjoin",
        "enable_mergejoin",
        "enable_nestloop",
        "enable_memoize",
        "enable_material",
    ];

    private static readonly string[] Values = ["on", "off"];

    /// <summary>No setting: what the tool passes.</summary>
    public static IReadOnlyList<string> None { get; } = [];

    /// <summary>The settings as given, when each is of the list.</summary>
    /// <exception cref="ArgumentException">
    /// A setting is not a name of the list with <c>on</c> or <c>off</c>.
    /// </exception>
    public static IReadOnlyList<string> Checked(IReadOnlyList<string> settings)
    {
        foreach (var setting in settings)
        {
            _ = SetText(setting);
        }

        return [.. settings];
    }

    /// <summary>The statement that sets one setting for the transaction it runs in.</summary>
    /// <exception cref="ArgumentException">
    /// The setting is not a name of the list with <c>on</c> or <c>off</c>.
    /// </exception>
    public static string SetText(string setting)
    {
        var parts = setting.Split('=');
        return
            parts.Length == 2
            && Names.Contains(parts[0], StringComparer.Ordinal)
            && Values.Contains(parts[1], StringComparer.Ordinal)
            ? $"SET LOCAL {parts[0]} = {parts[1]}"
            : throw new ArgumentException(
                $"'{setting}' is no setting of the planner that an explain may carry.",
                nameof(setting)
            );
    }
}
