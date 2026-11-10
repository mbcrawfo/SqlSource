namespace SqlSource.Settings;

/// <summary>
/// A value for each generator parameter: the index of its word in its
/// <see cref="GeneratorParameterDefinition.Words" />, four bits in the parameter's
/// <see cref="GeneratorParameterDefinition.Slot" />.  A small value, so that the settings that hold it compare cheaply
/// and the generator's cache costs nothing.
/// </summary>
/// <param name="Packed">
/// The slots: the parameter of flag <c>1 &lt;&lt; i</c> at bits <c>4i</c> to <c>4i + 3</c>.
/// </param>
internal readonly record struct GeneratorParameterValues(ulong Packed)
{
    /// <summary>The value <c>on</c>, the first word of every parameter that is on or off.</summary>
    public const int On = 0;

    /// <summary>The value <c>off</c>, the second word of every parameter that is on or off.</summary>
    public const int Off = 1;

    private const ulong SlotBits = 0xF;

    /// <summary>The value of <paramref name="parameter" />, one flag.</summary>
    public int Get(GeneratorParameters parameter) => (int)((Packed >> Shift(parameter)) & SlotBits);

    /// <summary>These values with <paramref name="parameter" />, one flag, set to <paramref name="value" />.</summary>
    public GeneratorParameterValues With(GeneratorParameters parameter, int value)
    {
        var shift = Shift(parameter);
        return new((Packed & ~(SlotBits << shift)) | (((ulong)value & SlotBits) << shift));
    }

    /// <summary>Whether <paramref name="parameter" />, one flag, is <c>on</c>.</summary>
    public bool IsOn(GeneratorParameters parameter) => Get(parameter) == On;

    /// <summary>These values with every slot outside <paramref name="mask" /> zero.</summary>
    public GeneratorParameterValues Keep(GeneratorParameters mask) => new(Packed & SlotsOf(mask));

    /// <summary>
    /// These values in the slots of <paramref name="mask" />, and <paramref name="lower" />'s elsewhere.
    /// </summary>
    public GeneratorParameterValues Over(GeneratorParameters mask, GeneratorParameterValues lower)
    {
        var slots = SlotsOf(mask);
        return new((Packed & slots) | (lower.Packed & ~slots));
    }

    private static int Shift(GeneratorParameters parameter) => GeneratorParameterDefinition.SlotOf(parameter) * 4;

    // The bits of the slots of each flag of the mask.
    private static ulong SlotsOf(GeneratorParameters mask)
    {
        var slots = 0UL;
        var bits = (uint)mask;
        for (var slot = 0; bits != 0; slot++, bits >>= 1)
        {
            if ((bits & 1) != 0)
            {
                slots |= SlotBits << (slot * 4);
            }
        }

        return slots;
    }
}
