using System;
using System.Globalization;

namespace SqlSource.Testing;

// Runs the code of a using block under another current culture, and puts back the one it found when the block ends,
// so that a test of a culture that writes numbers or text its own way leaves the thread as it was.
internal sealed class CultureScope(CultureInfo culture) : IDisposable
{
    private readonly CultureInfo _found = Enter(culture);

    public void Dispose() => CultureInfo.CurrentCulture = _found;

    private static CultureInfo Enter(CultureInfo culture)
    {
        var found = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = culture;
        return found;
    }
}
