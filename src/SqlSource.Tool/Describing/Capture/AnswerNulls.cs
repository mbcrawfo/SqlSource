using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace SqlSource.Tool.Describing.Capture;

/// <summary>
/// Finds a null in an answer where the answer's record says there is none.
/// </summary>
/// <remarks>
/// System.Text.Json reads a member of a reference type that a capture lacks, or holds as null, as null whatever the
/// record declares; a missing member of a value type is its default, which this cannot find.  A capture may be
/// older than the record, so a nullable member that is absent is what it is; a member that is not nullable and is
/// null would fail the describer somewhere later, with nothing that names the capture.
/// </remarks>
internal static class AnswerNulls
{
    /// <summary>
    /// The path of the first such member, as the capture's file names it, <c>settings[0].value</c> for one,
    /// or null when the answer has none.
    /// </summary>
    public static string? Find(object answer) => FindIn(answer, string.Empty);

    private static string? FindIn(object value, string prefix)
    {
        var nullability = new NullabilityInfoContext();
        foreach (var member in value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (member.GetIndexParameters().Length > 0)
            {
                continue;
            }

            var path = prefix + JsonNamingPolicy.CamelCase.ConvertName(member.Name);
            if (Check(member.GetValue(value), nullability.Create(member), path) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static string? Check(object? value, NullabilityInfo declared, string path)
    {
        if (value is null)
        {
            return declared.ReadState == NullabilityState.NotNull ? path : null;
        }

        if (value is string || value.GetType().IsPrimitive)
        {
            return null;
        }

        if (value is IEnumerable items && declared.GenericTypeArguments is [var item])
        {
            var index = 0;
            foreach (var element in items)
            {
                var at = string.Create(CultureInfo.InvariantCulture, $"{path}[{index++}]");
                if (Check(element, item, at) is { } found)
                {
                    return found;
                }
            }

            return null;
        }

        return FindIn(value, path + ".");
    }
}
