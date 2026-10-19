using System.Buffers;

namespace SqlSource.Generation.Models;

/// <summary>The rules of a model's property names.</summary>
internal static class PropertyNames
{
    /// <summary>
    /// The PascalCase of a column's or a parameter's name: without one trailing <c>!</c> or <c>?</c>, which sets a
    /// column's nullability, split at each <c>_</c>, with each part's first character upper-cased by the invariant
    /// culture and the rest kept, joined.  <c>created_at</c> is <c>CreatedAt</c>, and <c>ID</c> stays <c>ID</c>.  A
    /// name that is already PascalCase is given back as it is.  The result can be empty, or no identifier.
    /// </summary>
    public static string PascalCase(string name)
    {
        var length = name.Length;
        if (length > 0 && name[length - 1] is '!' or '?')
        {
            length--;
        }

        if (length == 0)
        {
            return string.Empty;
        }

        var buffer = ArrayPool<char>.Shared.Rent(length);
        try
        {
            var written = 0;
            var changed = false;
            var startsAPart = true;
            for (var index = 0; index < length; index++)
            {
                var character = name[index];
                if (character == '_')
                {
                    startsAPart = true;
                    continue;
                }

                if (startsAPart)
                {
                    var upper = char.ToUpperInvariant(character);
                    changed |= upper != character;
                    character = upper;
                    startsAPart = false;
                }

                buffer[written++] = character;
            }

            return !changed && written == name.Length ? name : new string(buffer, 0, written);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    /// <summary>Whether <paramref name="name" /> can be a property's name: a C# identifier.</summary>
    public static bool IsIdentifier(string name) => SqlIdentifier.IsValid(name);

    /// <summary>
    /// Whether a record or <see cref="object" /> already declares or disallows a member of the name, compared
    /// ordinally, so that a property of it would not compile or would hide one.
    /// </summary>
    public static bool IsReserved(string name) =>
        name switch
        {
            "EqualityContract"
            or "Equals"
            or "GetHashCode"
            or "ToString"
            or "GetType"
            or "MemberwiseClone"
            or "Finalize"
            or "ReferenceEquals"
            or "PrintMembers"
            or "Deconstruct"
            or "Clone" => true,
            _ => false,
        };
}
