using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using SqlSource.Generation.Repositories;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generation.Repositories;

public sealed class MemberHidingTests
{
    // A function pointer can be invoked, so the call binds to it.  RepositoryShapes compiles no unsafe code.
    [Fact]
    public void IsHidden_MethodBesideAFunctionPointerField_IsHidden()
    {
        var compilation = GeneratorHarness
            .CreateCompilation([
                new SourceFile(
                    GeneratorHarness.SourcePath,
                    """
                    public abstract class RepositoryBase
                    {
                        protected object Open() => null!;
                    }

                    public unsafe class UserRepository : RepositoryBase
                    {
                        private new delegate*<object> Open = null;
                    }
                    """
                ),
            ])
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var repository = compilation.GetTypeByMetadataName("UserRepository").ShouldNotBeNull();
        var declaring = repository.BaseType.ShouldNotBeNull();
        var open = declaring.GetMembers("Open").ShouldHaveSingleItem();

        var isHidden = MemberHiding.IsHidden(
            open,
            declaring,
            MemberHiding.BaseTypesOf(repository),
            repository,
            compilation,
            throughType: null
        );

        isHidden.ShouldBeTrue();
    }
}
