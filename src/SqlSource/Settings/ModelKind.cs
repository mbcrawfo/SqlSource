namespace SqlSource.Settings;

/// <summary>
/// The shape of a generated model.  The generator's own form of <c>GeneratorModelType</c>, member for member.
/// </summary>
internal enum ModelKind
{
    /// <summary>A positional record.</summary>
    Record = 0,

    /// <summary>A sealed positional record.</summary>
    SealedRecord = 1,

    /// <summary>A class with a property for each member.</summary>
    Class = 2,

    /// <summary>A sealed class with a property for each member.</summary>
    SealedClass = 3,
}
