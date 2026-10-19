using Shouldly;
using Xunit;

namespace SqlSource.Tests.Postgres.EndToEnd;

// The project runs the generator when it builds: the attribute it declares is in the assembly.  No server is needed.
public sealed class ModelShapeTests
{
    [Fact]
    public void Build_Project_DeclaresTheGeneratorsAttribute() =>
        _ = typeof(ModelShapeTests).Assembly.GetType("SqlSource.SqlSourceGenerateAttribute").ShouldNotBeNull();
}
