using System;
using System.Threading;
using System.Threading.Tasks;

namespace SqlSource.Tool.Describing.Describers;

/// <summary>
/// How one engine opens a session, which <see cref="DescriberSession.OpenAsync{TRequest, TAnswer}" /> follows.
/// </summary>
/// <param name="Call">The name of the call that opens, as every capture records it.</param>
/// <param name="Request">What the call asks: nothing of a connection, so an empty request.</param>
/// <param name="Live">Asks the engine itself.</param>
/// <param name="ErrorOf">The failure of an answer that holds an error, or null.</param>
/// <param name="ServerOf">The server of an answer that holds no error.</param>
/// <param name="IsBelowTheFloor">Whether the server is older than the describer supports.</param>
/// <param name="VersionNotSupported">The failure of a server below the floor.</param>
/// <param name="Session">The session of a server that is not below the floor.</param>
/// <typeparam name="TRequest">The record of the call's request.</typeparam>
/// <typeparam name="TAnswer">The record of the call's answer.</typeparam>
internal sealed record SessionOpening<TRequest, TAnswer>(
    string Call,
    TRequest Request,
    Func<TRequest, CancellationToken, Task<TAnswer>> Live,
    Func<TAnswer, DescribeFailure?> ErrorOf,
    Func<TAnswer, ServerInfo> ServerOf,
    Func<TAnswer, ServerInfo, bool> IsBelowTheFloor,
    Func<ServerInfo, DescribeFailure> VersionNotSupported,
    Func<TAnswer, ServerInfo, IDescribeSession> Session
);
