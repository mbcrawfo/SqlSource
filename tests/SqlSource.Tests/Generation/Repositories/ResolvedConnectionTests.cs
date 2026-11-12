using Shouldly;
using SqlSource.Generation.Methods;
using SqlSource.Generation.Repositories;
using Xunit;

namespace SqlSource.Tests.Generation.Repositories;

// Which source of a database a method of each synchrony takes.
public sealed class ResolvedConnectionTests
{
    private static readonly ConnectionSource Asynchronous = TestModels.Source(
        ConnectionSourceKind.Factory,
        CandidateMember.Method,
        "OpenAsync",
        "this.OpenAsync",
        isAsync: true
    );

    private static readonly ConnectionSource Synchronous = TestModels.Source(
        ConnectionSourceKind.Factory,
        CandidateMember.Method,
        "Open",
        "this.Open"
    );

    [Fact]
    public void SourceFor_Asynchronous_IsTheAsynchronousSource() =>
        new ResolvedConnection("main", Asynchronous, Synchronous)
            .SourceFor(MethodSynchrony.Asynchronous)
            .ShouldBe(Asynchronous);

    [Fact]
    public void SourceFor_Synchronous_IsTheSynchronousSource() =>
        new ResolvedConnection("main", Asynchronous, Synchronous)
            .SourceFor(MethodSynchrony.Synchronous)
            .ShouldBe(Synchronous);

    // A database whose only source returns a task has none for a synchronous method.
    [Fact]
    public void SourceFor_SynchronousWithoutASynchronousSource_IsNull() =>
        new ResolvedConnection("main", Asynchronous, null).SourceFor(MethodSynchrony.Synchronous).ShouldBeNull();
}
