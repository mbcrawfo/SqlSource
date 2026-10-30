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

    /// <summary>
    /// A conversion that SqlSource itself generates for a library the project references, such as NodaTime's, whether
    /// the library's setting is on or not.  It has no location.
    /// </summary>
    Library,
}
