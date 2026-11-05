namespace SqlSource.Settings;

/// <summary>What a level says about one generator parameter it names.</summary>
internal enum GeneratorParameterState
{
    /// <summary>The parameter is on.</summary>
    On,

    /// <summary>The parameter is off.</summary>
    Off,

    /// <summary>The parameter has its default.</summary>
    Default,
}
