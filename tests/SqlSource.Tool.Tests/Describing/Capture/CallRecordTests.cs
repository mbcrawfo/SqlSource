using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Shouldly;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Capture;

// Every record that a describer passes to AskAsync is held to the rule of a capture: a sealed record of strings,
// integers, booleans, nullable ones, lists of them and records of the same kind, which is the same text after a
// round trip through the options of a capture.  A record is found by its name, which ends in Request or Answer, in
// the namespace of an engine's describer or of the describer of the tests.
public class CallRecordTests
{
    private static readonly string[] Namespaces = ["SqlSource.Tool.Describing.Postgres", "SqlSource.Tool.Tests"];

    private static readonly Type[] Scalars = [typeof(string), typeof(int), typeof(long), typeof(bool)];

    private static readonly Dictionary<string, Type> Found = new[]
    {
        typeof(ToolHost).Assembly,
        typeof(FakeDescriber).Assembly,
    }
        .SelectMany(static assembly => assembly.GetTypes())
        .Where(static type =>
            type.IsClass
            && type.Namespace is { } name
            && Namespaces.Contains(name)
            && (
                type.Name.EndsWith("Request", StringComparison.Ordinal)
                || type.Name.EndsWith("Answer", StringComparison.Ordinal)
            )
        )
        .ToDictionary(static type => type.FullName!, StringComparer.Ordinal);

    public static TheoryData<string> Records => [with(Found.Keys.Order(StringComparer.Ordinal))];

    // The search finds something: a rule that holds for no record holds nothing.
    [Fact]
    public void Records_OfTheDescriberOfTheTests_AreFound() =>
        Found
            .Values.Where(static type => type.Namespace == "SqlSource.Tool.Tests")
            .Select(static type => type.Name)
            .Order(StringComparer.Ordinal)
            .ShouldBe(["FakeDescribeAnswer", "FakeDescribeRequest", "FakeOpenAnswer", "FakeOpenRequest"]);

    [Theory]
    [MemberData(nameof(Records))]
    public void Record_EveryMember_IsOfAKindACaptureMayHold(string name) => ShouldBePlain(Found[name], []);

    // Once with every member filled, and once with every nullable one null and every list empty.
    [Theory]
    [MemberData(nameof(Records))]
    public void Record_RoundTripThroughTheOptions_IsTheSameText(string name)
    {
        var type = Found[name];
        foreach (var full in new[] { true, false })
        {
            var text = JsonSerializer.Serialize(
                Sample(type, nullable: false, full, depth: 0),
                type,
                CaptureJson.Options
            );

            var back = JsonSerializer.Deserialize(text, type, CaptureJson.Options);

            JsonSerializer.Serialize(back, type, CaptureJson.Options).ShouldBe(text);
            foreach (var member in Members(type))
            {
                text.ShouldContain($"\"{JsonNamingPolicy.CamelCase.ConvertName(member.Name)}\":");
            }
        }
    }

    // The seam's own types never cross the exchange, and the rule says why: each holds what a capture may not.
    [Theory]
    [InlineData(typeof(OpenRequest))]
    [InlineData(typeof(DescribeRequest))]
    [InlineData(typeof(DescribeFailure))]
    [InlineData(typeof(QueryDescription))]
    [InlineData(typeof(Outcome<QueryDescription, DescribeFailure>))]
    public void ShouldBePlain_TypeOfTheSeam_Fails(Type type) =>
        Should.Throw<ShouldAssertException>(() => ShouldBePlain(type, []));

    private static PropertyInfo[] Members(Type type) => type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

    private static bool IsList(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>);

    private static void ShouldBePlain(Type type, HashSet<Type> seen)
    {
        if (!seen.Add(type))
        {
            return;
        }

        (type.IsClass && type.IsSealed && type.BaseType == typeof(object)).ShouldBeTrue(
            $"{type.Name} is not a sealed class without a base"
        );
        _ = type.GetMethod("<Clone>$").ShouldNotBeNull($"{type.Name} is not a record");
        type.GetFields(BindingFlags.Public | BindingFlags.Instance).ShouldBeEmpty();
        foreach (var member in Members(type))
        {
            ShouldBeAllowed(member.PropertyType, $"{type.Name}.{member.Name}", seen);
        }
    }

    private static void ShouldBeAllowed(Type type, string member, HashSet<Type> seen)
    {
        var plain = Nullable.GetUnderlyingType(type) ?? type;
        if (Scalars.Contains(plain))
        {
            return;
        }

        if (IsList(plain))
        {
            ShouldBeAllowed(plain.GetGenericArguments()[0], member, seen);
            return;
        }

        plain.IsClass.ShouldBeTrue($"{member} is a {plain.Name}, which a capture may not hold");
        ShouldBePlain(plain, seen);
    }

    // A value of the type.  A nullable member is null in the sparse sample, and below a depth, where a record
    // that holds itself must end.
    private static object? Sample(Type type, bool nullable, bool full, int depth)
    {
        if (nullable && (!full || depth > 3))
        {
            return null;
        }

        var plain = Nullable.GetUnderlyingType(type) ?? type;
        if (plain == typeof(string))
        {
            return "text \"quoted\" <é>";
        }

        if (plain == typeof(int))
        {
            return 7;
        }

        if (plain == typeof(long))
        {
            return 7L;
        }

        if (plain == typeof(bool))
        {
            return true;
        }

        if (IsList(plain))
        {
            var item = plain.GetGenericArguments()[0];
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(item))!;
            if (full)
            {
                _ = list.Add(Sample(item, nullable: false, full, depth + 1));
            }

            return list;
        }

        var constructor = plain
            .GetConstructors()
            .OrderByDescending(static found => found.GetParameters().Length)
            .First();
        return constructor.Invoke([
            .. constructor
                .GetParameters()
                .Select(parameter => Sample(parameter.ParameterType, IsNullable(parameter), full, depth + 1)),
        ]);
    }

    private static bool IsNullable(ParameterInfo parameter) =>
        Nullable.GetUnderlyingType(parameter.ParameterType) is not null
        || new NullabilityInfoContext().Create(parameter).ReadState == NullabilityState.Nullable;
}
