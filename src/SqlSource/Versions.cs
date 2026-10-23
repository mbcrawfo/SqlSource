using System.Globalization;

namespace SqlSource;

/// <summary>The one rule for reading a number out of a version that a server or a driver reports.</summary>
internal static class Versions
{
    /// <summary>
    /// The major of <paramref name="version" />: its leading ASCII digits, so <c>13.12</c> is 13 and <c>18beta1</c> is
    /// 18.  Null when there is no version, when it starts with no digit, or when its digits are more than an
    /// <see cref="int" /> holds: the caller cannot tell what it is, and decides nothing from it.
    /// </summary>
    public static int? LeadingMajor(string? version)
    {
        if (version is null)
        {
            return null;
        }

        var length = 0;
        while (length < version.Length && version[length] is >= '0' and <= '9')
        {
            length++;
        }

        return int.TryParse(
            version.Substring(0, length),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var major
        )
            ? major
            : null;
    }
}
