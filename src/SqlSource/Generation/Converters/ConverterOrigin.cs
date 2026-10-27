namespace SqlSource.Generation.Converters;

/// <summary>Where a converter was found.</summary>
internal enum ConverterOrigin
{
    /// <summary>A method of the project's own source.</summary>
    Project,

    /// <summary>
    /// A method of a type of a referenced assembly that the type override, the column types or a <c>-- param:</c> or
    /// <c>-- column:</c> marker names.
    /// </summary>
    MappedType,
}
