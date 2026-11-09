using System;
using System.Collections.Generic;
using System.Text;
using SqlSource.Generation.Emitting;

namespace SqlSource.Generation.Methods;

/// <summary>
/// The arguments of a query's method, in their order: the receiver, which is the connection, the input model when
/// the query has parameters, a <c>string</c> for each token, then the transaction, the command's timeout and the
/// cancellation token.  A repository's method, which gets its own connection, has neither the receiver, the
/// transaction nor the timeout.  Everything that spells them reads this, so an argument is added here alone, as
/// <c>docs/architecture/generator.md#extending-query-methods</c> says.  Building it and walking it allocates
/// nothing, and its forms write into the builder.
/// </summary>
/// <param name="input">The input model, or null when the query has no parameters.</param>
/// <param name="tokens">The token names, as <see cref="MethodWriter.GetTokens" /> gives them.</param>
/// <param name="streams">
/// Whether the method streams its rows, as <see cref="QueryMethod.Streams" /> says, which changes the receiver's
/// documentation.
/// </param>
/// <param name="takesConnection">
/// Whether the method takes its connection, its transaction and its command's timeout: false for a repository's
/// method, whose <see cref="QueryMethod.Source" /> gives its connection.
/// </param>
internal readonly struct MethodArguments(MethodInput? input, List<string> tokens, bool streams, bool takesConnection)
{
    /// <summary>The receiver's name.</summary>
    public const string Connection = "connection";

    /// <summary>The input model's name.</summary>
    public const string Parameters = "parameters";

    /// <summary>The transaction's name.</summary>
    public const string Transaction = "transaction";

    /// <summary>The command timeout's name.</summary>
    public const string CommandTimeout = "commandTimeout";

    /// <summary>The cancellation token's name.</summary>
    public const string CancellationToken = "cancellationToken";

    private const string Await = "await";

    private const string ConnectionType = "global::System.Data.Common.DbConnection";

    private const string CancellationType = "global::System.Threading.CancellationToken";

    private const string ParametersXml = "The query's parameters.";

    // The attribute that marks the iterator's token as the one WithCancellation gives.
    private const string EnumeratorCancellation = "[global::System.Runtime.CompilerServices.EnumeratorCancellation] ";

    // The names of the arguments that every method can have, which no token may take, with an input or without.
    private static readonly string[] ReservedNames =
    [
        Connection,
        Parameters,
        Transaction,
        CommandTimeout,
        CancellationToken,
    ];

    private static readonly MethodArgument Receiver = new(
        ArgumentRole.Receiver,
        Connection,
        ConnectionType,
        null,
        "The connection to run the query on.  It is opened when it is closed, and closed again when the method opened "
            + "it."
    );

    private static readonly MethodArgument StreamReceiver = new(
        ArgumentRole.Receiver,
        Connection,
        ConnectionType,
        null,
        "The connection to run the query on.  When it is closed, it is opened when the loop starts and closed again "
            + "when the loop ends."
    );

    private static readonly MethodArgument TransactionArgument = new(
        ArgumentRole.Transaction,
        Transaction,
        "global::System.Data.Common.DbTransaction?",
        "null",
        "The transaction to run the query in, or null."
    );

    private static readonly MethodArgument CommandTimeoutArgument = new(
        ArgumentRole.CommandTimeout,
        CommandTimeout,
        "int?",
        "null",
        "The command's timeout in seconds, or null for the driver's."
    );

    private static readonly MethodArgument CancellationArgument = new(
        ArgumentRole.Cancellation,
        CancellationToken,
        CancellationType,
        "default",
        "Cancels the query."
    );

    private readonly MethodInput? _input = input;

    private readonly List<string> _tokens = tokens;

    private readonly bool _streams = streams;

    /// <summary>
    /// Whether the method takes its connection, its transaction and its command's timeout, which a repository's method
    /// does not.
    /// </summary>
    public bool TakesConnection { get; } = takesConnection;

    /// <summary>
    /// The arguments of the method, with its tokens as <see cref="MethodWriter.GetTokens" /> gives them.
    /// </summary>
    public static MethodArguments Of(QueryMethod method, List<string> tokens) =>
        new(method.Input, tokens, method.Streams, method.Source is null);

    /// <summary>Whether no token may take the name, since an argument of some method has it.  Case counts.</summary>
    public static bool IsReserved(string name) => Array.IndexOf(ReservedNames, name) >= 0;

    /// <summary>
    /// Appends the name as the method writes it: <c>@await</c> for <c>await</c>, which an asynchronous method cannot
    /// use as an identifier, and any other as it is.
    /// </summary>
    public static void AppendName(StringBuilder builder, string name) =>
        _ = (name == Await ? builder.Append('@') : builder).Append(name);

    /// <summary>
    /// How each token's argument is written in the method, in the order of <paramref name="tokens" />: the list itself
    /// unless a token is <c>await</c>, which is written <c>@await</c>.
    /// </summary>
    public static List<string> SpellTokens(List<string> tokens)
    {
        if (!tokens.Contains(Await))
        {
            return tokens;
        }

        var spelled = new List<string>(tokens.Count);
        foreach (var token in tokens)
        {
            spelled.Add(token == Await ? "@" + Await : token);
        }

        return spelled;
    }

    /// <summary>Walks the arguments in their order.</summary>
    public Enumerator GetEnumerator() => new(this);

    /// <summary>
    /// Appends the method's arguments, each on its own line at <paramref name="indent" /> and one level more, and
    /// the parenthesis that closes them.
    /// </summary>
    /// <param name="builder">The source being built.</param>
    /// <param name="indent">The whitespace that starts the method's line.</param>
    /// <param name="host">
    /// Where the method goes, which writes <c>this</c> before the connection of an extension.
    /// </param>
    public void AppendDeclaration(StringBuilder builder, string indent, MethodHost host) =>
        Append(builder, indent, host.IsExtension, isIterator: false);

    /// <summary>
    /// Appends the arguments of a streaming method's iterator as <see cref="AppendDeclaration" /> does, without
    /// <c>this</c> or a default, and with the cancellation token marked as the one <c>WithCancellation</c> gives.
    /// </summary>
    /// <param name="builder">The source being built.</param>
    /// <param name="indent">The whitespace that starts the iterator's line.</param>
    public void AppendIteratorDeclaration(StringBuilder builder, string indent) =>
        Append(builder, indent, isExtension: false, isIterator: true);

    /// <summary>Appends the arguments of the call to the iterator: each name, in order, joined by <c>, </c>.</summary>
    public void AppendCall(StringBuilder builder)
    {
        var first = true;
        foreach (var argument in this)
        {
            if (!first)
            {
                _ = builder.Append(", ");
            }

            AppendName(builder, argument.Name);
            first = false;
        }
    }

    private void Append(StringBuilder builder, string indent, bool isExtension, bool isIterator)
    {
        foreach (var argument in this)
        {
            _ = builder.Append(indent).Append(GeneratedText.Indent);
            if (isExtension && argument.Role == ArgumentRole.Receiver)
            {
                _ = builder.Append("this ");
            }

            if (isIterator && argument.Role == ArgumentRole.Cancellation)
            {
                _ = builder.Append(EnumeratorCancellation);
            }

            _ = builder.Append(argument.TypeText).Append(' ');
            AppendName(builder, argument.Name);
            if (!isIterator && argument.DefaultText is { } defaultText)
            {
                _ = builder.Append(" = ").Append(defaultText);
            }

            _ = builder.Append(argument.Role == ArgumentRole.Cancellation ? ")\n" : ",\n");
        }
    }

    /// <summary>
    /// Walks the arguments: the receiver and the input when the method has them, the tokens, then the transaction and
    /// the timeout when it has them, and the cancellation token.
    /// </summary>
    public struct Enumerator
    {
        private readonly MethodArguments _arguments;

        // The number of arguments before the tokens, the receiver's and the input's that the method has, and after
        // them.
        private readonly int _leading;

        private readonly int _trailing;

        private int _position;

        internal Enumerator(MethodArguments arguments)
        {
            _arguments = arguments;
            _leading = (arguments.TakesConnection ? 1 : 0) + (arguments._input is null ? 0 : 1);
            _trailing = arguments.TakesConnection ? 3 : 1;
            _position = -1;
            Current = default;
        }

        /// <summary>The argument at the position the walk has reached.</summary>
        public MethodArgument Current { get; private set; }

        /// <summary>Moves to the next argument, or gives false after the last.</summary>
        public bool MoveNext()
        {
            var tokens = _arguments._tokens.Count;
            var count = _leading + tokens + _trailing;
            if (_position + 1 >= count)
            {
                _position = count;
                return false;
            }

            _position++;
            var afterTokens = _position - _leading - tokens;
            Current = afterTokens switch
            {
                < 0 when _position >= _leading => new MethodArgument(
                    ArgumentRole.Token,
                    _arguments._tokens[_position - _leading],
                    "string",
                    null,
                    null
                ),
                < 0 when _position == 0 && _arguments.TakesConnection => _arguments._streams
                    ? StreamReceiver
                    : Receiver,
                < 0 => new MethodArgument(
                    ArgumentRole.Input,
                    Parameters,
                    _arguments._input!.TypeText,
                    null,
                    ParametersXml
                ),
                _ when afterTokens == _trailing - 1 => CancellationArgument,
                0 => TransactionArgument,
                _ => CommandTimeoutArgument,
            };
            return true;
        }
    }
}
