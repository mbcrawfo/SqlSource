namespace SqlSource.Settings.Libraries;

/// <summary>What a level says about a <see cref="SqlLibrary" />.</summary>
internal enum LibraryState
{
    /// <summary>The library's types are used.</summary>
    On,

    /// <summary>The library's types are not used.</summary>
    Off,

    /// <summary>
    /// The level takes the library out of the levels below it, so that the engine's own choice applies, as a type
    /// override's <c>default</c> does.
    /// </summary>
    Default,
}
