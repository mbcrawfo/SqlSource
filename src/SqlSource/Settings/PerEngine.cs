using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlSource.Settings;

/// <summary>
/// A value of each <see cref="DatabaseEngine" />, compared by value so that a pipeline value that holds one caches.
/// <see cref="PerEngine.Of{T}" /> builds it from a function that reaches a switch over the engine, so a new engine is
/// IDE0072 there.  <c>default</c>, <see cref="PerEngine.Only{T}" /> and <see cref="With" /> leave the other slots
/// <c>default(T)</c>, so they are for values where that means the source says nothing.  A container whose slots are
/// all <c>default(T)</c> holds no array, and equals <c>default</c>.
/// </summary>
/// <typeparam name="T">The type of the value of an engine.</typeparam>
internal readonly struct PerEngine<T> : IEquatable<PerEngine<T>>
{
    // One slot for each engine, by its number; null when every slot is default(T).
    private readonly T[]? _slots;

    private PerEngine(T[] slots)
    {
        _slots = slots;
    }

    /// <summary>The value of an engine; <c>default(T)</c> in a container that holds no array.</summary>
    /// <param name="engine">The engine.</param>
    public T this[DatabaseEngine engine]
    {
        get
        {
            if ((uint)(int)engine >= (uint)DatabaseEngines.All.Length)
            {
                throw UnexpectedEnumValueException.Of(engine);
            }

            return _slots is null ? default! : _slots[(int)engine];
        }
    }

    public static bool operator ==(PerEngine<T> left, PerEngine<T> right) => left.Equals(right);

    public static bool operator !=(PerEngine<T> left, PerEngine<T> right) => !left.Equals(right);

    /// <summary>The container with <paramref name="value" /> in the slot of <paramref name="engine" />.</summary>
    /// <param name="engine">The engine.</param>
    /// <param name="value">Its new value.</param>
    public PerEngine<T> With(DatabaseEngine engine, T value)
    {
        if ((uint)(int)engine >= (uint)DatabaseEngines.All.Length)
        {
            throw UnexpectedEnumValueException.Of(engine);
        }

        var slots = new T[DatabaseEngines.All.Length];
        _slots?.CopyTo(slots, 0);
        slots[(int)engine] = value;
        return Create(slots);
    }

    /// <summary>The container of <paramref name="selector" /> applied to each slot.</summary>
    /// <typeparam name="TResult">The type of the new values.</typeparam>
    /// <param name="selector">The function from a value to its replacement.</param>
    public PerEngine<TResult> Select<TResult>(Func<T, TResult> selector)
    {
        var slots = new TResult[DatabaseEngines.All.Length];
        foreach (var engine in DatabaseEngines.All)
        {
            slots[(int)engine] = selector(this[engine]);
        }

        return PerEngine<TResult>.Create(slots);
    }

    /// <summary>The container of <paramref name="combine" /> applied to each pair of slots, this one's first.</summary>
    /// <typeparam name="TOther">The type of the other container's values.</typeparam>
    /// <typeparam name="TResult">The type of the new values.</typeparam>
    /// <param name="other">The other container.</param>
    /// <param name="combine">The function from a pair of values to the new one.</param>
    public PerEngine<TResult> Zip<TOther, TResult>(PerEngine<TOther> other, Func<T, TOther, TResult> combine)
    {
        var slots = new TResult[DatabaseEngines.All.Length];
        foreach (var engine in DatabaseEngines.All)
        {
            slots[(int)engine] = combine(this[engine], other[engine]);
        }

        return PerEngine<TResult>.Create(slots);
    }

    public bool Equals(PerEngine<T> other)
    {
        var (comparer, self) = (EqualityComparer<T>.Default, this);
        return DatabaseEngines.All.All(engine => comparer.Equals(self[engine], other[engine]));
    }

    public override bool Equals(object? obj) => obj is PerEngine<T> other && Equals(other);

    public override int GetHashCode()
    {
        var comparer = EqualityComparer<T>.Default;
        var hash = 17;
        foreach (var engine in DatabaseEngines.All)
        {
            var slot = this[engine];
            hash = unchecked((hash * 31) + (slot is null ? 0 : comparer.GetHashCode(slot)));
        }

        return hash;
    }

    // The one way every other member builds a container: the default one, with no array, when every slot is
    // default(T), so a container that says nothing allocates nothing.
    internal static PerEngine<T> Create(T[] slots)
    {
        var comparer = EqualityComparer<T>.Default;
        return slots.Any(slot => !comparer.Equals(slot, default!)) ? new PerEngine<T>(slots) : default;
    }
}

/// <summary>The ways to build a <see cref="PerEngine{T}" />.</summary>
internal static class PerEngine
{
    /// <summary>
    /// The container of <paramref name="valueOf" /> for each engine, called once for each in the order of
    /// <see cref="DatabaseEngines.All" />.  The function reaches a switch over the engine, so each engine must be
    /// decided.
    /// </summary>
    /// <typeparam name="T">The type of the value of an engine.</typeparam>
    /// <param name="valueOf">The function from an engine to its value.</param>
    public static PerEngine<T> Of<T>(Func<DatabaseEngine, T> valueOf)
    {
        var slots = new T[DatabaseEngines.All.Length];
        foreach (var engine in DatabaseEngines.All)
        {
            slots[(int)engine] = valueOf(engine);
        }

        return PerEngine<T>.Create(slots);
    }

    /// <summary>
    /// The container that holds <paramref name="value" /> for <paramref name="engine" /> and <c>default(T)</c> for
    /// every other, for values where that means the source says nothing.
    /// </summary>
    /// <typeparam name="T">The type of the value of an engine.</typeparam>
    /// <param name="engine">The engine.</param>
    /// <param name="value">Its value.</param>
    public static PerEngine<T> Only<T>(DatabaseEngine engine, T value) => default(PerEngine<T>).With(engine, value);
}
