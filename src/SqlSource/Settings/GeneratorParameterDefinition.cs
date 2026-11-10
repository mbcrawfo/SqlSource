using System;
using System.Collections.Generic;

namespace SqlSource.Settings;

/// <summary>
/// One generator parameter: its name, the words it takes as its value, and its default among them.  A word is stored
/// as its index in <see cref="Words" />, in the parameter's <see cref="Slot" /> of
/// <see cref="GeneratorParameterValues" />, which holds at most <see cref="MaxWords" /> words a parameter.
/// </summary>
internal sealed class GeneratorParameterDefinition
{
    /// <summary>
    /// The most words a parameter takes: a slot of <see cref="GeneratorParameterValues" /> is four bits.
    /// </summary>
    public const int MaxWords = 16;

    public GeneratorParameterDefinition(
        string name,
        GeneratorParameters parameter,
        IReadOnlyList<string> words,
        int @default
    )
    {
        if (words.Count > MaxWords)
        {
            throw new ArgumentException($"A generator parameter takes at most {MaxWords} words.", nameof(words));
        }

        if (@default < 0 || @default >= words.Count)
        {
            throw new ArgumentException("The default is not one of the words.", nameof(@default));
        }

        Slot = SlotOf(parameter);
        Name = name;
        Parameter = parameter;
        Words = words;
        Default = @default;
        ValuesHelp = ".  The values of " + name + " are " + string.Join(", ", words) + " and default";
    }

    /// <summary>The name a <c>-- generator:</c> line or a settings file writes.</summary>
    public string Name { get; }

    /// <summary>The parameter's flag, which names it in a level's choices.</summary>
    public GeneratorParameters Parameter { get; }

    /// <summary>The words the parameter takes after <c>=</c>, beside <c>default</c>.</summary>
    public IReadOnlyList<string> Words { get; }

    /// <summary>The index in <see cref="Words" /> of the parameter's value when no level names it.</summary>
    public int Default { get; }

    /// <summary>The position of <see cref="Parameter" />'s one bit: its slot of four bits in a value.</summary>
    public int Slot { get; }

    /// <summary>The end of the message of a value that is not one of <see cref="Words" />, which lists them.</summary>
    public string ValuesHelp { get; }

    /// <summary>A parameter that is on or off: the words <c>on</c> and <c>off</c>.</summary>
    public static GeneratorParameterDefinition Switch(string name, GeneratorParameters parameter, bool onByDefault) =>
        new(name, parameter, ["on", "off"], onByDefault ? GeneratorParameterValues.On : GeneratorParameterValues.Off);

    /// <summary>Finds <paramref name="text" /> among <see cref="Words" />, ignoring case.</summary>
    public bool TryReadValue(ReadOnlySpan<char> text, out int value)
    {
        for (var index = 0; index < Words.Count; index++)
        {
            if (text.Equals(Words[index].AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                value = index;
                return true;
            }
        }

        value = 0;
        return false;
    }

    /// <summary>The position of the one bit of <paramref name="parameter" />.</summary>
    internal static int SlotOf(GeneratorParameters parameter)
    {
        var bits = (uint)parameter;
        if (bits == 0 || (bits & (bits - 1)) != 0)
        {
            throw new ArgumentException("A generator parameter is one flag.", nameof(parameter));
        }

        var slot = 0;
        while ((bits >>= 1) != 0)
        {
            slot++;
        }

        return slot;
    }
}
