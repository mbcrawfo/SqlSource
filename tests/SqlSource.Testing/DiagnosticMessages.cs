using System.Globalization;
using Microsoft.CodeAnalysis;

namespace SqlSource.Testing;

// A diagnostic's message as its descriptor formats it, in the invariant culture.
internal static class DiagnosticMessages
{
    public static string Format(DiagnosticDescriptor descriptor, params object?[] arguments) =>
        string.Format(
            CultureInfo.InvariantCulture,
            descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture),
            arguments
        );
}
