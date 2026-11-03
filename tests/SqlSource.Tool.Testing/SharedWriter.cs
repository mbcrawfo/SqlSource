using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SqlSource.Tool.Testing;

// A writer to a string that a test reads while a run writes to it from threads of its own.  A line is one write, so a
// reader never sees half of it, and an asynchronous write is done before it returns, as a StringWriter's is.
internal sealed class SharedWriter : TextWriter
{
    private readonly StringBuilder _text = new();

    public SharedWriter()
        : base(CultureInfo.InvariantCulture)
    {
        NewLine = "\n";
    }

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value)
    {
        lock (_text)
        {
            _ = _text.Append(value);
        }
    }

    public override void Write(string? value)
    {
        lock (_text)
        {
            _ = _text.Append(value);
        }
    }

    public override void Write(char[] buffer, int index, int count)
    {
        lock (_text)
        {
            _ = _text.Append(buffer, index, count);
        }
    }

    public override void WriteLine(string? value)
    {
        lock (_text)
        {
            _ = _text.Append(value).Append(CoreNewLine);
        }
    }

    public override Task WriteAsync(string? value)
    {
        Write(value);
        return Task.CompletedTask;
    }

    public override Task WriteLineAsync(string? value)
    {
        WriteLine(value);
        return Task.CompletedTask;
    }

    public override Task WriteLineAsync()
    {
        WriteLine();
        return Task.CompletedTask;
    }

    public override Task FlushAsync() => Task.CompletedTask;

    public override string ToString()
    {
        lock (_text)
        {
            return _text.ToString();
        }
    }
}
