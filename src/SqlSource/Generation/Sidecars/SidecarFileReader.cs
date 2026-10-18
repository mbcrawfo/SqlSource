using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Diagnostics;
using SqlSource.Snapshot.Reading;

namespace SqlSource.Generation.Sidecars;

/// <summary>
/// Reads one sidecar and turns each position the reader keeps into a <see cref="LocationInfo" />, while the text is
/// in hand.
/// </summary>
internal static class SidecarFileReader
{
    private const char ByteOrderMark = '\uFEFF';

    /// <summary>
    /// Reads the sidecar of <paramref name="input" />.  A file whose text cannot be had is malformed at its start.
    /// The reader takes a text without a byte order mark, so one is left out of what it reads, and every position
    /// is moved past it again: a position is one of the file as it is.
    /// </summary>
    public static SidecarFile Read(SidecarInput input, CancellationToken cancellationToken)
    {
        var path = input.File.Path;
        var text = input.File.GetText(cancellationToken);
        SidecarReadResult result;
        var offset = 0;
        if (text is null)
        {
            text = SourceText.From(string.Empty);
            result = SidecarReadResult.Malformed(new SidecarError(SidecarErrorKind.InvalidJson, default, null));
        }
        else
        {
            offset = text.Length > 0 && text[0] == ByteOrderMark ? 1 : 0;
            result = SidecarReader.Read(
                offset == 0 ? text.ToString() : text.ToString(TextSpan.FromBounds(offset, text.Length))
            );
        }

        return new SidecarFile(
            input.SqlPath,
            path,
            result,
            LocationInfo.From(path, text, default),
            result.Error is { } error ? Locate(path, text, offset, error.Span) : null,
            LocateEntries(path, text, offset, result)
        );
    }

    private static EquatableArray<LocationInfo> LocateEntries(
        string path,
        SourceText text,
        int offset,
        SidecarReadResult result
    )
    {
        if (result.Sidecar is not { } sidecar)
        {
            return EquatableArray<LocationInfo>.Empty;
        }

        var locations = ImmutableArray.CreateBuilder<LocationInfo>(sidecar.Queries.Count);
        foreach (var entry in sidecar.Queries)
        {
            locations.Add(Locate(path, text, offset, entry.NameSpan));
        }

        return new EquatableArray<LocationInfo>(locations.MoveToImmutable());
    }

    // A span of the text that was read, as a position in the file, whose first offset characters were left out.
    private static LocationInfo Locate(string path, SourceText text, int offset, TextSpan span) =>
        LocationInfo.From(path, text, new TextSpan(span.Start + offset, span.Length));
}
