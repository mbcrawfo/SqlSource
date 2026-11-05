using System;
using System.Collections.Generic;

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

        T[]? slots = null;
        foreach (var each in DatabaseEngines.All)
        {
            Store(ref slots, each, each == engine ? value : this[each]);
        }

        return From(slots);
    }

    /// <summary>The container of <paramref name="selector" /> applied to each slot.</summary>
    /// <typeparam name="TResult">The type of the new values.</typeparam>
    /// <param name="selector">The function from a value to its replacement.</param>
    public PerEngine<TResult> Select<TResult>(Func<T, TResult> selector)
    {
        TResult[]? slots = null;
        foreach (var engine in DatabaseEngines.All)
        {
            PerEngine<TResult>.Store(ref slots, engine, selector(this[engine]));
        }

        return PerEngine<TResult>.From(slots);
    }

    /// <summary>The container of <paramref name="combine" /> applied to each pair of slots, this one's first.</summary>
    /// <typeparam name="TOther">The type of the other container's values.</typeparam>
    /// <typeparam name="TResult">The type of the new values.</typeparam>
    /// <param name="other">The other container.</param>
    /// <param name="combine">The function from a pair of values to the new one.</param>
    public PerEngine<TResult> Zip<TOther, TResult>(PerEngine<TOther> other, Func<T, TOther, TResult> combine)
    {
        TResult[]? slots = null;
        foreach (var engine in DatabaseEngines.All)
        {
            PerEngine<TResult>.Store(ref slots, engine, combine(this[engine], other[engine]));
        }

        return PerEngine<TResult>.From(slots);
    }

    public bool Equals(PerEngine<T> other)
    {
        var comparer = EqualityComparer<T>.Default;
        for (var at = 0; at < DatabaseEngines.All.Length; at++)
        {
            // A container with no array holds default(T) in every slot.
            var mine = _slots is null ? default! : _slots[at];
            var theirs = other._slots is null ? default! : other._slots[at];
            if (!comparer.Equals(mine, theirs))
            {
                return false;
            }
        }

        return true;
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

    // The two steps by which every member builds a container, so that one that says nothing allocates nothing: the
    // array is made at the first slot that is not default(T), and a build that finds none never makes it.  The
    // builder keeps `slots` local, and hands it to `From` once, so the array is never shared.
    internal static void Store(ref T[]? slots, DatabaseEngine engine, T value)
    {
        if (slots is null)
        {
            if (EqualityComparer<T>.Default.Equals(value, default!))
            {
                return;
            }

            slots = new T[DatabaseEngines.All.Length];
        }

        slots[(int)engine] = value;
    }

    // The default container, with no array, when `slots` was never made.
    internal static PerEngine<T> From(T[]? slots) => slots is null ? default : new PerEngine<T>(slots);
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
        T[]? slots = null;
        foreach (var engine in DatabaseEngines.All)
        {
            PerEngine<T>.Store(ref slots, engine, valueOf(engine));
        }

        return PerEngine<T>.From(slots);
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
