using SqlSource.Settings;

namespace SqlSource.Tests;

/// <summary>The values of the generator parameters, written as the switches that are on.</summary>
internal static class SwitchValues
{
    /// <summary>
    /// Each parameter that is on or off <c>on</c> when it is in <paramref name="on" />, and <c>off</c> otherwise; a
    /// parameter of other words at its default.
    /// </summary>
    public static GeneratorParameterValues Of(GeneratorParameters on)
    {
        var values = GeneratorParameterValues.Defaults;
        foreach (var definition in GeneratorParameterList.Definitions)
        {
            if (definition.Words is not ["on", "off"])
            {
                continue;
            }

            var isOn = (on & definition.Parameter) != GeneratorParameters.None;
            values = values.With(
                definition.Parameter,
                isOn ? GeneratorParameterValues.On : GeneratorParameterValues.Off
            );
        }

        return values;
    }
}
