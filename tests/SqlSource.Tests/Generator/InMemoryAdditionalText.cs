using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Tests.Generator;

// A .sql file as the compiler hands it to a generator, without a file on disk.  A null text is a file that cannot be
// read.
internal sealed class InMemoryAdditionalText(string path, string? text) : AdditionalText
{
    public override string Path => path;

    public override SourceText? GetText(CancellationToken cancellationToken = default) =>
        text is null ? null : SourceText.From(text);
}
