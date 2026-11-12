using System;
using Shouldly;
using SqlSource.Generation.Methods;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Generation.Methods;

// Which forms a query's output can give it, by its method-type: a repository's as a methods query's.
public sealed class MethodNamesTests
{
    // The cases name the enums' members, which are internal: the output, the method-type, and whether it can have a
    // synchronous and an asynchronous method.
    [Theory]
    [InlineData("Methods", "Async", false, true)]
    [InlineData("Methods", "Sync", true, false)]
    [InlineData("Methods", "Both", true, true)]
    [InlineData("Repository", "Async", false, true)]
    [InlineData("Repository", "Sync", true, false)]
    [InlineData("Repository", "Both", true, true)]
    [InlineData("Sql", "Both", false, false)]
    [InlineData("Models", "Both", false, false)]
    public void HasSynchronousAndHasAsynchronous_OfEachOutputAndMethodType_FollowTheMethodType(
        string output,
        string methodType,
        bool hasSynchronous,
        bool hasAsynchronous
    )
    {
        var markers = new SettingsLevel
        {
            Output = Enum.Parse<OutputKind>(output),
            Parameters = GeneratorParameterChoices.None.With(
                GeneratorParameters.MethodType,
                (int)Enum.Parse<MethodType>(methodType)
            ),
        };

        var settings = QuerySettings.Resolve(markers, SettingsLevel.None);

        MethodNames.HasSynchronous(settings).ShouldBe(hasSynchronous);
        MethodNames.HasAsynchronous(settings).ShouldBe(hasAsynchronous);
    }
}
