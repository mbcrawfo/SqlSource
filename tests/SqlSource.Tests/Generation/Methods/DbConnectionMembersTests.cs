using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using Shouldly;
using SqlSource.Generation.Methods;
using Xunit;

namespace SqlSource.Tests.Generation.Methods;

public sealed class DbConnectionMembersTests
{
    // The list is written out, since the generator runs on the compiler's runtime, so it is compared with the runtime
    // the tests run on: every public member of DbConnection and of the types it derives from, but its constructors and
    // the accessors of its properties and events.
    [Fact]
    public void Names_OfTheRuntime_AreThePublicMembersOfDbConnection()
    {
        var names = new List<string>();
        for (var type = typeof(DbConnection); type is not null; type = type.BaseType)
        {
            names.AddRange(
                type.GetMembers(
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly
                    )
                    .Where(member => member is not ConstructorInfo and not MethodInfo { IsSpecialName: true })
                    .Select(member => member.Name)
            );
        }

        DbConnectionMembers.Names.ShouldBe(names.Distinct().OrderBy(name => name, StringComparer.Ordinal));
    }
}
