namespace SqlSource.Settings;

/// <summary>
/// What one settings level says about the generator parameters: the ones it names, and which of those are on.  A
/// level that names nothing leaves every parameter to the levels below, and one that names an option decides it
/// whatever they say.
/// </summary>
/// <param name="Named">The parameters this level gives a state.</param>
/// <param name="On">
/// The parameters this level turns on, always a subset of <paramref name="Named" />, so that equal choices compare
/// equal.
/// </param>
internal readonly record struct GeneratorParameterChoices(GeneratorParameters Named, GeneratorParameters On)
{
    /// <summary>The choices of a level that names nothing.</summary>
    public static GeneratorParameterChoices None => default;

    /// <summary>
    /// The choices that name every parameter with its default, so that they hide every state of the levels below.
    /// </summary>
    public static GeneratorParameterChoices Reset => new(GeneratorParameterList.All, GeneratorParameterList.Defaults);

    /// <summary>Whether no parameter is named.</summary>
    public bool IsEmpty => Named == GeneratorParameters.None;

    /// <summary>These choices with <paramref name="parameter" /> named and given <paramref name="state" />.</summary>
    public GeneratorParameterChoices With(GeneratorParameters parameter, GeneratorParameterState state)
    {
        var on = state switch
        {
            GeneratorParameterState.On => parameter,
            GeneratorParameterState.Off => GeneratorParameters.None,
            GeneratorParameterState.Default => GeneratorParameterList.Defaults & parameter,
            _ => throw UnexpectedEnumValueException.Of(state),
        };

        return new GeneratorParameterChoices(Named | parameter, (On & ~parameter) | on);
    }

    /// <summary>
    /// These choices over <paramref name="lower" />: this level's state for each parameter it names, and
    /// <paramref name="lower" />'s for the rest.
    /// </summary>
    public GeneratorParameterChoices Over(GeneratorParameterChoices lower) =>
        new(Named | lower.Named, On | (lower.On & ~Named));

    /// <summary>Whether <paramref name="parameter" /> is on, or null when these choices do not name it.</summary>
    public bool? StateOf(GeneratorParameters parameter) =>
        (Named & parameter) == GeneratorParameters.None ? null : (On & parameter) != GeneratorParameters.None;

    /// <summary>The parameters that are on: the named states, over the defaults of the rest.</summary>
    public GeneratorParameters Resolve() => On | (GeneratorParameterList.Defaults & ~Named);
}
