using SqlSource.Diagnostics;

namespace SqlSource.Generation;

/// <summary>
/// The name that an <c>-- input-model:</c> or <c>-- output-model:</c> marker gives, with where the marker is.
/// </summary>
/// <param name="Name">The full name or the name the marker gives.</param>
/// <param name="Location">The whole marker comment in the file.</param>
internal sealed record ModelMarker(string Name, LocationInfo Location);
