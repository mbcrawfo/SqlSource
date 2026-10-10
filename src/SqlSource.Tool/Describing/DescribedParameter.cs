using SqlSource.Snapshot;

namespace SqlSource.Tool.Describing;

/// <summary>
/// A parameter as a describer typed it.
/// </summary>
/// <param name="Name">The bare name, in any case.</param>
/// <param name="Type">The engine's type, or null when the engine gave none.</param>
/// <param name="TypeSource">Where the type came from.  Provenance.</param>
internal sealed record DescribedParameter(string Name, SidecarType? Type, string? TypeSource);
