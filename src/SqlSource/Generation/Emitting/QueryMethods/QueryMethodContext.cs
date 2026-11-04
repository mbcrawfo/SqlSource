using System.Collections.Generic;
using System.Text;
using SqlSource.Generation.Methods;

namespace SqlSource.Generation.Emitting.QueryMethods;

/// <summary>
/// What every part of a query's method is written from, built once as <see cref="QueryMethodWriter" /> starts the
/// method and passed <c>in</c> to each helper, so that a helper takes its indent and the values of its own call alone.
/// It is a struct, so building it allocates nothing.
/// </summary>
internal readonly struct QueryMethodContext(
    StringBuilder builder,
    QueryMethod method,
    MethodHost host,
    List<string> tokens,
    List<string> spelledTokens,
    MethodArguments arguments,
    QueryMethodLocals locals,
    ExecutionCapabilities capabilities,
    bool streams
)
{
    /// <summary>The source being built.</summary>
    public StringBuilder Builder { get; } = builder;

    /// <summary>The query's method.  Its <see cref="QueryMethod.Output" /> is set when its shape reads rows.</summary>
    public QueryMethod Method { get; } = method;

    /// <summary>Where the method goes and how visible it is, the method's <see cref="QueryMethod.Host" />.</summary>
    public MethodHost Host { get; } = host;

    /// <summary>The names of the query's tokens, as <see cref="MethodWriter.GetTokens" /> gives them.</summary>
    public List<string> Tokens { get; } = tokens;

    /// <summary>
    /// The tokens as the list that builds the SQL writes them, as <see cref="MethodArguments.SpellTokens" /> gives
    /// them.
    /// </summary>
    public List<string> SpelledTokens { get; } = spelledTokens;

    /// <summary>The method's arguments.</summary>
    public MethodArguments Arguments { get; } = arguments;

    /// <summary>The names of the method's locals, and of the connection and the transaction it runs on.</summary>
    public QueryMethodLocals Locals { get; } = locals;

    /// <summary>What the engine needs of the method beyond running its command and reading its rows.</summary>
    public ExecutionCapabilities Capabilities { get; } = capabilities;

    /// <summary>Whether the method streams its rows, as <see cref="QueryMethod.Streams" /> says.</summary>
    public bool Streams { get; } = streams;
}
