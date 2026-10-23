using System;

namespace SqlSource.Tool.Describing.Describers;

/// <summary>Reads the chain of an exception's inner exceptions.</summary>
internal static class ExceptionChain
{
    /// <summary>
    /// The first exception of type <typeparamref name="T" /> in the chain of inner exceptions, the exception itself
    /// first, or null when there is none.
    /// </summary>
    /// <typeparam name="T">The type of exception to find.</typeparam>
    /// <param name="exception">The exception the chain starts at.</param>
    public static T? Find<T>(Exception exception)
        where T : Exception
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is T found)
            {
                return found;
            }
        }

        return null;
    }
}
