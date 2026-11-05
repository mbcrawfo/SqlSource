using System.Collections.Immutable;
using System.Text;

namespace SqlSource.Tool.Bundles.Renaming;

/// <summary>
/// Builds a <see cref="RenamedText" /> from an original, part by part in order: each part of the original is copied
/// or replaced, and the builder records where each of its characters went.
/// </summary>
internal sealed class RenamedTextBuilder(string original)
{
    private readonly string _original = original;
    private readonly StringBuilder _text = new(original.Length);

    // For each char of the original, and its end, the index of the renamed char it became.
    private readonly int[] _offsets = new int[original.Length + 1];
    private int _done;

    /// <summary>Copies the original from where the last part ended to <paramref name="end" />.</summary>
    public void CopyTo(int end)
    {
        for (var index = _done; index < end; index++)
        {
            _offsets[index] = _text.Length + index - _done;
        }

        _ = _text.Append(_original, _done, end - _done);
        _done = end;
    }

    /// <summary>
    /// Replaces the original from where the last part ended to <paramref name="end" /> by a text; each of its
    /// characters goes to the replacement's start.
    /// </summary>
    public void ReplaceTo(int end, string replacement)
    {
        for (var index = _done; index < end; index++)
        {
            _offsets[index] = _text.Length;
        }

        _ = _text.Append(replacement);
        _done = end;
    }

    /// <summary>The renamed text, once every part of the original is copied or replaced.</summary>
    public RenamedText Build()
    {
        CopyTo(_original.Length);
        _offsets[_original.Length] = _text.Length;
        var text = _text.ToString();
        var renamedPoints = CodePointsBefore(text);
        var offsets = ImmutableArray.CreateBuilder<int>(_original.Length + 1);
        for (var index = 0; index < _original.Length; index++)
        {
            if (StartsCodePoint(_original, index))
            {
                offsets.Add(renamedPoints[_offsets[index]]);
            }
        }

        offsets.Add(renamedPoints[text.Length]);
        return new RenamedText(text, offsets.ToImmutable());
    }

    // For each char of the text, and its end, the index of the code point it belongs to.
    private static int[] CodePointsBefore(string text)
    {
        var points = new int[text.Length + 1];
        var point = -1;
        for (var index = 0; index < text.Length; index++)
        {
            if (StartsCodePoint(text, index))
            {
                point++;
            }

            points[index] = point;
        }

        points[text.Length] = point + 1;
        return points;
    }

    private static bool StartsCodePoint(string text, int index) =>
        !(index > 0 && char.IsLowSurrogate(text[index]) && char.IsHighSurrogate(text[index - 1]));
}
