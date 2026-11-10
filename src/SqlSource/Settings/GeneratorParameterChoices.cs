namespace SqlSource.Settings;

/// <summary>
/// What one settings level says about the generator parameters: the ones it names, and the value it gives each.  A
/// level that names nothing leaves every parameter to the levels below, and one that names a parameter decides it
/// whatever they say.
/// </summary>
/// <remarks>
/// Build a value through <see cref="None" />, <see cref="Reset" />, the <c>With</c> methods and <see cref="Over" />,
/// never the positional constructor, which does not hold <paramref name="Values" /> to <paramref name="Named" />.
/// </remarks>
/// <param name="Named">The parameters this level gives a value.</param>
/// <param name="Values">
/// The value of each parameter of <paramref name="Named" />, and 0 in every other slot, so that equal choices compare
/// equal.
/// </param>
internal readonly record struct GeneratorParameterChoices(GeneratorParameters Named, GeneratorParameterValues Values)
{
    /// <summary>The choices of a level that names nothing.</summary>
    public static GeneratorParameterChoices None => default;

    /// <summary>
    /// The choices that name every parameter with its default, so that they hide every value of the levels below.
    /// </summary>
    public static GeneratorParameterChoices Reset => new(GeneratorParameterList.All, GeneratorParameterValues.Defaults);

    /// <summary>Whether no parameter is named.</summary>
    public bool IsEmpty => Named == GeneratorParameters.None;

    /// <summary>
    /// These choices with each parameter of <paramref name="parameters" /> named and given <paramref name="state" />:
    /// <c>on</c>, <c>off</c>, or its own default.
    /// </summary>
    public GeneratorParameterChoices With(GeneratorParameters parameters, GeneratorParameterState state)
    {
        var choices = this;
        foreach (var definition in GeneratorParameterList.Definitions)
        {
            if ((parameters & definition.Parameter) == GeneratorParameters.None)
            {
                continue;
            }

            var value = state switch
            {
                GeneratorParameterState.On => GeneratorParameterValues.On,
                GeneratorParameterState.Off => GeneratorParameterValues.Off,
                GeneratorParameterState.Default => definition.Default,
                _ => throw UnexpectedEnumValueException.Of(state),
            };
            choices = choices.With(definition.Parameter, value);
        }

        return choices;
    }

    /// <summary>
    /// These choices with <paramref name="parameter" />, one flag, named and given <paramref name="value" />, the index
    /// of one of its words.
    /// </summary>
    public GeneratorParameterChoices With(GeneratorParameters parameter, int value) =>
        new(Named | parameter, Values.With(parameter, value));

    /// <summary>
    /// These choices over <paramref name="lower" />: this level's value for each parameter it names, and
    /// <paramref name="lower" />'s for the rest.
    /// </summary>
    public GeneratorParameterChoices Over(GeneratorParameterChoices lower) =>
        new(Named | lower.Named, Values.Over(Named, lower.Values));

    /// <summary>The value of <paramref name="parameter" />, or null when these choices do not name it.</summary>
    public int? ValueOf(GeneratorParameters parameter) =>
        (Named & parameter) == GeneratorParameters.None ? null : Values.Get(parameter);

    /// <summary>Whether <paramref name="parameter" /> is on, or null when these choices do not name it.</summary>
    public bool? StateOf(GeneratorParameters parameter) =>
        ValueOf(parameter) is { } value ? value == GeneratorParameterValues.On : null;

    /// <summary>The value of every parameter: the named values, over the defaults of the rest.</summary>
    public GeneratorParameterValues Resolve() => Values.Over(Named, GeneratorParameterValues.Defaults);
}
