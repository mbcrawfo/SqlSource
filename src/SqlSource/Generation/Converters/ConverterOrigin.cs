namespace SqlSource.Generation.Converters;

/// <summary>Where a converter was found.</summary>
internal enum ConverterOrigin
{
    /// <summary>A method of the project's own source.</summary>
    Project,

    /// <summary>A method of a type that the type override names, of a referenced assembly.</summary>
    MappedType,
}
