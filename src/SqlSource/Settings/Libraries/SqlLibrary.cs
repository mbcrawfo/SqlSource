namespace SqlSource.Settings.Libraries;

/// <summary>
/// A library whose types SqlSource can use for columns and parameters, in the order of the library types.  The
/// lower-cased name of a member is what a settings file and a marker write, and the manual spells it in camel case.
/// </summary>
internal enum SqlLibrary
{
    /// <summary>NodaTime's date and time types.</summary>
    NodaTime,
}
