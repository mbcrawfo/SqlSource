using System;
using SqlSource.Diagnostics;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Describing;

/// <summary>
/// An exception that a describer threw, which is a bug of the describer, as the <c>SQLSRC200</c> to report: with
/// the connection's value already taken out.  It holds no inner exception, whose message may hold the value.
/// <c>Cli.RunAsync</c> reports <see cref="Diagnostic" /> and the run ends.
/// </summary>
/// <remarks>
/// <para>
/// It derives from <see cref="InvalidOperationException" /> and not from <see cref="Exception" />: S3871 wants an
/// exception that derives from <see cref="Exception" /> to be public, and CA1515 wants no public type in the tool.
/// </para>
/// <para>
/// The standard constructors are there because CA1032 asks for them.  They give the <c>SQLSRC200</c> of their
/// message, and the one with an inner exception drops it.
/// </para>
/// </remarks>
internal sealed class DescriberFaultException : InvalidOperationException
{
    public DescriberFaultException(ToolDiagnostic diagnostic)
        : base("A describer failed.")
    {
        Diagnostic = diagnostic;
    }

    public DescriberFaultException()
        : this("A describer failed.") { }

    public DescriberFaultException(string message)
        : base(message)
    {
        Diagnostic = ToolDiagnostic.Create(
            ToolDiagnostics.UnexpectedFailure,
            typeof(DescriberFaultException).FullName ?? nameof(DescriberFaultException),
            message
        );
    }

    public DescriberFaultException(string message, Exception innerException)
        : this(message)
    {
        // Dropped, as the class says.
        _ = innerException;
    }

    public ToolDiagnostic Diagnostic { get; }
}
