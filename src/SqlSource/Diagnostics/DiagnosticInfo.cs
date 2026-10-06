using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace SqlSource.Diagnostics;

/// <summary>
/// A diagnostic as data.  It becomes a <see cref="Diagnostic" /> in an output step of the generator pipeline.
/// </summary>
/// <param name="Descriptor">What is wrong.  Always a member of <see cref="SqlDiagnostics" />.</param>
/// <param name="Location">Where it is.</param>
/// <param name="Arguments">The text the descriptor's message quotes.</param>
internal sealed record DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    LocationInfo Location,
    EquatableArray<string> Arguments
)
{
    public static DiagnosticInfo Create(
        DiagnosticDescriptor descriptor,
        LocationInfo location,
        params string[] arguments
    ) => new(descriptor, location, new EquatableArray<string>(ImmutableArray.Create(arguments)));

    public Diagnostic ToDiagnostic() =>
        Diagnostic.Create(Descriptor, Location.ToLocation(), Arguments.Cast<object>().ToArray());
}
