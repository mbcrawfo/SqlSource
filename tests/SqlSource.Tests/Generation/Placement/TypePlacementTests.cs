using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation;
using SqlSource.Generation.Emitting;
using SqlSource.Generation.Placement;
using SqlSource.Parsing;
using SqlSource.Settings;
using SqlSource.Testing.Snapshot;
using Xunit;

namespace SqlSource.Tests.Generation.Placement;

public sealed class TypePlacementTests
{
    private const string SettingsPath = "/app/sqlsource.json";

    private const string Reminder = "Write -- visibility: or -- member-location: on the query.";

    [Fact]
    public void Of_QueriesWithNothingWritten_TakeTheDefaultOfTheirOutput()
    {
        var files = Files(File("Users.sql", Query("GetUser", output: OutputKind.Sql), Query("ListUsers")));

        var (placement, diagnostics) = Analyse(TestModels.Type(), files);

        diagnostics.ShouldBeEmpty();
        placement.Usable.ShouldBe([true]);
        placement.NestedClasses.ShouldBe([new NestedClassPlacement("Sql", Visibility.Private)]);
        placement.EmptyNestedClass.ShouldBeNull();
        PlacementOf(placement, 0, 0)
            .ShouldBe(new QueryPlacement(MemberLocation.NestedClass, Visibility.Private, "Sql"));
        PlacementOf(placement, 0, 1)
            .ShouldBe(new QueryPlacement(MemberLocation.ExtensionClass, Visibility.Internal, null));
    }

    [Theory]
    [InlineData(true, "Public")]
    [InlineData(false, "Internal")]
    public void Of_ClassWithNoVisibility_IsPublicOnAPublicTypeAndInternalOnAnother(bool isPublic, string expected)
    {
        var files = Files(File("Users.sql", Query("GetUser", output: OutputKind.Sql, location: MemberLocation.Class)));

        var (placement, diagnostics) = Analyse(TestModels.Type(isPublic: isPublic), files);

        diagnostics.ShouldBeEmpty();
        placement.NestedClasses.ShouldBeEmpty();
        PlacementOf(placement, 0, 0).Visibility.ToString().ShouldBe(expected);
        PlacementOf(placement, 0, 0).NestedClass.ShouldBeNull();
    }

    // The nested class is ignored away from a nested class.
    [Fact]
    public void Of_NestedClassWrittenUnderClass_IsIgnored()
    {
        var files = Files(
            File(
                "Users.sql",
                Query("GetUser", output: OutputKind.Sql, location: MemberLocation.Class, nestedClass: "Users")
            )
        );

        var (placement, diagnostics) = Analyse(TestModels.Type(), files);

        diagnostics.ShouldBeEmpty();
        placement.NestedClasses.ShouldBeEmpty();
        PlacementOf(placement, 0, 0).ShouldBe(new QueryPlacement(MemberLocation.Class, Visibility.Public, null));
    }

    [Fact]
    public void Of_FileThatMixesLocations_GivesEachNestedClassInTheOrderOfItsFirstQuery()
    {
        var files = Files(
            File(
                "Users.sql",
                Query("GetCount", output: OutputKind.Sql, location: MemberLocation.Class),
                Query("GetOrder", output: OutputKind.Sql, nestedClass: "Orders", visibility: Visibility.Internal),
                Query("GetUser", output: OutputKind.Sql, nestedClass: "Users"),
                Query("ListOrders", nestedClass: "Orders", location: MemberLocation.NestedClass)
            )
        );

        var (placement, diagnostics) = Analyse(TestModels.Type(), files);

        diagnostics.ShouldBeEmpty();
        placement.NestedClasses.ShouldBe([
            new NestedClassPlacement("Orders", Visibility.Internal),
            new NestedClassPlacement("Users", Visibility.Private),
        ]);
        PlacementOf(placement, 0, 3)
            .ShouldBe(new QueryPlacement(MemberLocation.NestedClass, Visibility.Internal, "Orders"));
    }

    [Theory]
    [InlineData("Sql", false)]
    [InlineData("UserRepository", true)]
    public void Of_ConstantNamedLikeItsContainer_ReportsSqlsrc009AndLeavesOutItsFile(string name, bool onType)
    {
        var files = Files(
            File("Users.sql", Query(name, output: OutputKind.Sql, location: onType ? MemberLocation.Class : null)),
            File("Orders.sql", Query("GetOrder", output: OutputKind.Sql, at: At("Orders.sql", 0)))
        );

        var (placement, diagnostics) = Analyse(TestModels.Type(), files);

        diagnostics.ShouldBe([
            DiagnosticInfo.Create(
                SqlDiagnostics.QueryNamedLikeContainingType,
                At("Users.sql", 0),
                "UserRepository",
                name
            ),
        ]);
        placement.Usable.ShouldBe([false, true]);
    }

    // A method in a nested class named like it is CS0542, with Async under async-method-suffix.
    [Theory]
    [InlineData("Users", false)]
    [InlineData("UsersAsync", true)]
    public void Of_MethodInANestedClassNamedLikeIt_ReportsSqlsrc009(string nestedClass, bool asyncSuffix)
    {
        var files = Files(
            File(
                "Users.sql",
                Query(
                    "Users",
                    location: MemberLocation.NestedClass,
                    nestedClass: nestedClass,
                    parameters: asyncSuffix ? GeneratorParameters.AsyncMethodSuffix : GeneratorParameters.None
                )
            )
        );

        var (placement, diagnostics) = Analyse(TestModels.Type(), files);

        diagnostics.ShouldBe([
            DiagnosticInfo.Create(
                SqlDiagnostics.QueryNamedLikeContainingType,
                At("Users.sql", 0),
                "UserRepository",
                "Users"
            ),
        ]);
        placement.Usable.ShouldBe([false]);
    }

    // The method planner judges a method's name on the type.
    [Fact]
    public void Of_MethodOnTheTypeNamedLikeIt_IsNoErrorHere()
    {
        var files = Files(File("Users.sql", Query("UserRepository", location: MemberLocation.Class)));

        var (placement, diagnostics) = Analyse(TestModels.Type(), files);

        diagnostics.ShouldBeEmpty();
        placement.Usable.ShouldBe([true]);
    }

    [Fact]
    public void Of_QueryNameInTwoFiles_ReportsSqlsrc008AtTheSecond()
    {
        var files = Files(
            File("Users.sql", Query("GetUser", output: OutputKind.Sql)),
            File("Orders.sql", Query("GetUser", output: OutputKind.Sql, at: At("Orders.sql", 0)))
        );

        var (placement, diagnostics) = Analyse(TestModels.Type(), files);

        diagnostics.ShouldBe([
            DiagnosticInfo.Create(
                SqlDiagnostics.DuplicateQueryName,
                At("Orders.sql", 0),
                "GetUser",
                "Users.sql",
                "UserRepository"
            ),
        ]);
        placement.Usable.ShouldBe([true, false]);
    }

    [Fact]
    public void Of_KeptFormWithAnErrorWhereCommentsAreKept_ReportsItAndLeavesOutTheFile()
    {
        var error = DiagnosticInfo.Create(SqlDiagnostics.TokenCrossesComment, At("Users.sql", 1), "table");
        var query = Query("GetUser", output: OutputKind.Sql, parameters: GeneratorParameters.KeepComments) with
        {
            KeptErrors = TestSidecars.Of(error),
        };

        var (placement, diagnostics) = Analyse(TestModels.Type(), Files(File("Users.sql", query)));

        diagnostics.ShouldBe([error]);
        placement.Usable.ShouldBe([false]);
    }

    // A folder-wide memberLocation of the extension class reaches every query below it.
    [Fact]
    public void Of_SettingsFileExtensionClassOverSqlQueries_ReportsSqlsrc122AtEachQueryNamingTheFile()
    {
        var level = new SettingsLevel
        {
            MemberLocation = new Placed<MemberLocation>(MemberLocation.ExtensionClass, SettingsFile),
        };
        var files = Files(
            File(
                "Users.sql",
                Query("GetUser", output: OutputKind.Sql),
                Query("ListUsers", output: OutputKind.Models, at: At("Users.sql", 3))
            )
        );

        var (placement, diagnostics) = Analyse(TestModels.Type(), files, level);

        diagnostics.ShouldBe([
            DiagnosticInfo.Create(
                SqlDiagnostics.MemberLocationNotFit,
                At("Users.sql", 0),
                "GetUser",
                "App.UserRepository",
                "the settings file '/app/sqlsource.json'",
                "sql"
            ),
            DiagnosticInfo.Create(
                SqlDiagnostics.MemberLocationNotFit,
                At("Users.sql", 3),
                "ListUsers",
                "App.UserRepository",
                "the settings file '/app/sqlsource.json'",
                "models"
            ),
        ]);
        placement.Usable.ShouldBe([false]);
        placement.NestedClasses.ShouldBeEmpty();
    }

    [Fact]
    public void Of_QueryMarkerOfTheExtensionClassOnASqlQuery_ReportsSqlsrc122AtTheMarker()
    {
        var query = Query(
            "GetUser",
            output: OutputKind.Sql,
            location: MemberLocation.ExtensionClass,
            place: SettingPlace.Query
        ) with
        {
            MemberLocationMarker = At("Users.sql", 1),
        };

        var (_, diagnostics) = Analyse(TestModels.Type(), Files(File("Users.sql", query)));

        diagnostics.ShouldBe([
            DiagnosticInfo.Create(
                SqlDiagnostics.MemberLocationNotFit,
                At("Users.sql", 1),
                "GetUser",
                "App.UserRepository",
                "its own marker",
                "sql"
            ),
        ]);
    }

    // A header's visibility reaches every query of the file, including one whose location it does not fit.
    [Fact]
    public void Of_HeaderPrivateWithAMethodsQuery_ReportsSqlsrc123NamingTheHeader()
    {
        var files = Files(
            File(
                "Users.sql",
                Query("GetUser", output: OutputKind.Sql, visibility: Visibility.Private),
                Query("ListUsers", visibility: Visibility.Private, at: At("Users.sql", 3))
            )
        );

        var (placement, diagnostics) = Analyse(TestModels.Type(), files);

        diagnostics.ShouldBe([
            DiagnosticInfo.Create(
                SqlDiagnostics.VisibilityNotFit,
                At("Users.sql", 3),
                "ListUsers",
                "App.UserRepository",
                "private",
                "the file header",
                "extension-class",
                "an extension method is public or internal"
            ),
        ]);
        placement.Usable.ShouldBe([false]);
        diagnostics[0]
            .ToDiagnostic()
            .GetMessage(System.Globalization.CultureInfo.InvariantCulture)
            .ShouldEndWith(Reminder);
    }

    // Placement does not see the query's models, so it leaves a public extension method of a type that is not public
    // to the method planner, which refuses it only for an internal model.
    [Fact]
    public void Of_PublicExtensionMethodOfAnInternalType_IsUsable()
    {
        var query = Query("GetUser", visibility: Visibility.Public, place: SettingPlace.Query) with
        {
            VisibilityMarker = At("Users.sql", 2),
            MemberLocationMarker = At("Users.sql", 1),
        };

        var (placement, diagnostics) = Analyse(TestModels.Type(isPublic: false), Files(File("Users.sql", query)));

        diagnostics.ShouldBeEmpty();
        placement.Usable.ShouldBe([true]);
    }

    // Only what C# forbids is an error: protected on a static class (CS1057) or a struct (CS0666), and a protected
    // class nested in a struct.
    [Theory]
    [InlineData(
        "Class",
        "class",
        true,
        false,
        "'App.UserRepository' is a static class, which cannot have protected members"
    )]
    [InlineData(
        "Class",
        "struct",
        false,
        true,
        "'App.UserRepository' is a struct, which cannot have protected members"
    )]
    [InlineData(
        "Class",
        "record struct",
        false,
        true,
        "'App.UserRepository' is a struct, which cannot have protected members"
    )]
    [InlineData(
        "NestedClass",
        "struct",
        false,
        true,
        "'App.UserRepository' is a struct, which cannot have protected members"
    )]
    [InlineData("Class", "class", false, true, null)]
    [InlineData("Class", "record", false, true, null)]
    [InlineData("NestedClass", "class", true, false, null)]
    public void Of_Protected_IsAnErrorOnlyWhereCSharpForbidsIt(
        string locationName,
        string keyword,
        bool isStatic,
        bool isSealed,
        string? reason
    )
    {
        var location = System.Enum.Parse<MemberLocation>(locationName);
        var type = TestModels.Type(
            types: [new TypeDeclaration(keyword, "UserRepository", "UserRepository", string.Empty, 0)],
            isStatic: isStatic,
            isSealed: isSealed
        );
        var files = Files(
            File(
                "Users.sql",
                Query("GetUser", output: OutputKind.Sql, location: location, visibility: Visibility.Protected)
            )
        );

        var (placement, diagnostics) = Analyse(type, files);

        if (reason is null)
        {
            diagnostics.ShouldBeEmpty();
            placement.Usable.ShouldBe([true]);
            PlacementOf(placement, 0, 0).Visibility.ShouldBe(Visibility.Protected);
            return;
        }

        diagnostics.ShouldBe([
            DiagnosticInfo.Create(
                SqlDiagnostics.VisibilityNotFit,
                At("Users.sql", 0),
                "GetUser",
                "App.UserRepository",
                "protected",
                "the file header",
                PlacementChecks.TextOf(location),
                reason
            ),
        ]);
        placement.Usable.ShouldBe([false]);
    }

    // Another file's query writes the class Sql public, which its constant can be.  The method of the query that writes
    // no visibility is public in it too, which placement allows: the method planner refuses it only for an internal
    // model.  Every file is usable, and the class keeps the visibility written for it.
    [Fact]
    public void Of_NestedClassMadePublicByAnotherFileOfATypeReachableAsProtected_IsUsable()
    {
        var files = Files(
            File("Users.sql", Query("GetName", output: OutputKind.Sql, visibility: Visibility.Public)),
            File("Orders.sql", Query("GetOrder", location: MemberLocation.NestedClass, at: At("Orders.sql", 2)))
        );

        var (placement, diagnostics) = Analyse(ReachableAsProtected(), files);

        diagnostics.ShouldBeEmpty();
        placement.Usable.ShouldBe([true, true]);
        placement.NestedClasses.ShouldBe([new NestedClassPlacement("Sql", Visibility.Public)]);
    }

    // The same with both queries in one file.
    [Fact]
    public void Of_NestedClassMadePublicByAnotherQuery_IsUsable()
    {
        var method = Query("GetOrder", location: MemberLocation.NestedClass, at: At("Users.sql", 2)) with
        {
            MemberLocationMarker = At("Users.sql", 3),
        };
        var files = Files(
            File("Users.sql", Query("GetName", output: OutputKind.Sql, visibility: Visibility.Protected), method)
        );

        var (placement, diagnostics) = Analyse(ReachableAsProtected(), files);

        diagnostics.ShouldBeEmpty();
        placement.Usable.ShouldBe([true]);
        placement.NestedClasses.ShouldBe([new NestedClassPlacement("Sql", Visibility.Protected)]);
    }

    // Where a query method could not take or return the type's internal models: a public or protected method of a type
    // reachable as protected, on it or in its nested class, and a public extension method of a type that is not
    // public.  Not an internal or private method, not one of a type that is not reachable so, and not a public
    // extension method of a public type.
    [Theory]
    [InlineData("Class", "Public", false, true, "protected")]
    [InlineData("Class", "Protected", false, true, "protected")]
    [InlineData("NestedClass", "Public", false, true, "protected")]
    [InlineData("NestedClass", "Protected", false, true, "protected")]
    [InlineData("Class", "Internal", false, true, null)]
    [InlineData("NestedClass", "Private", false, true, null)]
    [InlineData("Class", "Public", false, false, null)]
    [InlineData("NestedClass", "Protected", false, false, null)]
    [InlineData("ExtensionClass", "Public", false, false, "extension")]
    [InlineData("ExtensionClass", "Public", true, false, null)]
    [InlineData("ExtensionClass", "Internal", false, false, null)]
    public void MethodModelsReason_OfEachLocationAndVisibility_RefusesOnlyWhereModelsWouldNotCompile(
        string locationName,
        string visibilityName,
        bool isPublic,
        bool isReachableAsProtected,
        string? refusal
    )
    {
        var type = TestModels.Type(isPublic: isPublic, isReachableAsProtected: isReachableAsProtected);

        var reason = PlacementChecks.MethodModelsReason(
            type,
            System.Enum.Parse<MemberLocation>(locationName),
            System.Enum.Parse<Visibility>(visibilityName)
        );

        reason.ShouldBe(
            refusal switch
            {
                "protected" => "'App.UserRepository' is reachable as protected, so its models are internal, which a "
                    + "method reachable from another assembly cannot take or return",
                "extension" => "'App.UserRepository' is not public, so its models are internal, which a public "
                    + "extension method cannot take or return",
                _ => null,
            }
        );
    }

    // A written visibility beats a default: the class of the two files is public, whichever file comes first.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Of_SharedNestedClassWithOneWrittenVisibility_TakesIt(bool writtenFirst)
    {
        var written = File("Users.sql", Query("GetUser", output: OutputKind.Sql, visibility: Visibility.Public));
        var unwritten = File("Orders.sql", Query("GetOrder", output: OutputKind.Sql, at: At("Orders.sql", 0)));
        var files = writtenFirst ? Files(written, unwritten) : Files(unwritten, written);

        var (placement, diagnostics) = Analyse(TestModels.Type(), files);

        diagnostics.ShouldBeEmpty();
        placement.Usable.ShouldBe([true, true]);
        placement.NestedClasses.ShouldBe([new NestedClassPlacement("Sql", Visibility.Public)]);
        PlacementOf(placement, writtenFirst ? 1 : 0, 0).Visibility.ShouldBe(Visibility.Public);
    }

    [Fact]
    public void Of_SharedNestedClassWithTwoWrittenVisibilities_ReportsSqlsrc124AndDropsBothFiles()
    {
        var second = Query(
            "GetOrder",
            output: OutputKind.Sql,
            visibility: Visibility.Internal,
            place: SettingPlace.Query,
            at: At("Orders.sql", 0)
        ) with
        {
            VisibilityMarker = At("Orders.sql", 1),
        };
        var files = Files(
            File("Users.sql", Query("GetUser", output: OutputKind.Sql, visibility: Visibility.Public)),
            File("Orders.sql", second),
            File("Other.sql", Query("GetOther", output: OutputKind.Sql, nestedClass: "Other", at: At("Other.sql", 0)))
        );

        var (placement, diagnostics) = Analyse(TestModels.Type(), files);

        diagnostics.ShouldBe([
            DiagnosticInfo.Create(
                SqlDiagnostics.NestedClassVisibilities,
                At("Orders.sql", 1),
                "Sql",
                "App.UserRepository",
                "internal",
                "its own marker",
                "GetOrder",
                "the file header",
                "public",
                "GetUser"
            ),
        ]);
        placement.Usable.ShouldBe([false, false, true]);
        placement.NestedClasses.ShouldBe([new NestedClassPlacement("Other", Visibility.Private)]);
    }

    // The type's member takes the name that a file's header gives its nested class: once at the attribute, and the
    // file's members go, while the type's other files keep theirs.
    [Fact]
    public void Of_NestedClassNamedLikeAMemberOfTheType_ReportsSqlsrc007OnceAndDropsItsFiles()
    {
        var type = TestModels.Type(takenNames: ["Queries", "UserRepository"]);
        var files = Files(
            File(
                "Users.sql",
                Query("GetUser", output: OutputKind.Sql, nestedClass: "Queries"),
                Query("ListUsers", output: OutputKind.Sql, nestedClass: "Queries")
            ),
            File("Orders.sql", Query("GetOrder", output: OutputKind.Sql, at: At("Orders.sql", 0)))
        );

        var (placement, diagnostics) = Analyse(type, files);

        diagnostics.ShouldBe([
            DiagnosticInfo.Create(
                SqlDiagnostics.NestedClassNameTaken,
                TestModels.AttributeLocation,
                "UserRepository",
                "Queries"
            ),
        ]);
        placement.Usable.ShouldBe([false, true]);
        placement.NestedClasses.ShouldBe([new NestedClassPlacement("Sql", Visibility.Private)]);
    }

    [Fact]
    public void Of_ConstantOnTheTypeNamedLikeANestedClass_ReportsSqlsrc125AndDropsItsFile()
    {
        var files = Files(
            File("Users.sql", Query("GetUser", output: OutputKind.Sql, nestedClass: "Users")),
            File(
                "Orders.sql",
                Query("Users", output: OutputKind.Sql, location: MemberLocation.Class, at: At("Orders.sql", 0))
            )
        );

        var (placement, diagnostics) = Analyse(TestModels.Type(), files);

        diagnostics.ShouldBe([
            DiagnosticInfo.Create(
                SqlDiagnostics.MemberNameTaken,
                At("Orders.sql", 0),
                "Users",
                "App.UserRepository",
                "a nested class of the type's queries has that name"
            ),
        ]);
        placement.Usable.ShouldBe([true, false]);
    }

    // A constant or token method on the type cannot take a name the type declares, which TakenNames holds with the
    // type's own name: its file is left out, and the other file kept.
    [Fact]
    public void Of_ConstantOnTheTypeNamedLikeWhatTheTypeDeclares_ReportsSqlsrc125AndDropsItsFile()
    {
        var files = Files(
            File("Users.sql", Query("Get", output: OutputKind.Sql, location: MemberLocation.Class)),
            File(
                "Orders.sql",
                Query("GetOrder", output: OutputKind.Sql, location: MemberLocation.Class, at: At("Orders.sql", 0))
            )
        );

        var (placement, diagnostics) = Analyse(TestModels.Type(takenNames: ["Get", "UserRepository"]), files);

        diagnostics.ShouldBe([
            DiagnosticInfo.Create(
                SqlDiagnostics.MemberNameTaken,
                At("Users.sql", 0),
                "Get",
                "App.UserRepository",
                "the type declares a member, a nested type or a type parameter of that name"
            ),
        ]);
        placement.Usable.ShouldBe([false, true]);
    }

    // Only a member on the type is checked against what the type declares: a constant in a nested class is apart
    // from it, and the method planner judges a method's name on the type.
    // An internal enum cannot be the parameter of a public test method, so a row says which of the two it is.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Of_DeclaredNameOffTheTypeOrForAMethod_IsNoErrorHere(bool isMethod)
    {
        var query = isMethod
            ? Query("Get", output: OutputKind.Methods, location: MemberLocation.Class)
            : Query("Get", output: OutputKind.Sql, location: MemberLocation.NestedClass);
        var files = Files(File("Users.sql", query));

        var (placement, diagnostics) = Analyse(TestModels.Type(takenNames: ["Get", "UserRepository"]), files);

        diagnostics.ShouldBeEmpty();
        placement.Usable.ShouldBe([true]);
    }

    // A method on the type named like a nested class is the method planner's SQLSRC274.
    [Fact]
    public void Of_MethodOnTheTypeNamedLikeANestedClass_IsNoErrorHere()
    {
        var files = Files(
            File(
                "Users.sql",
                Query("GetUser", output: OutputKind.Sql, nestedClass: "Users"),
                Query("Users", location: MemberLocation.Class)
            )
        );

        var (_, diagnostics) = Analyse(TestModels.Type(), files);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Of_NoUsableQuery_WritesTheEmptyNestedClassOfTheFirstFile()
    {
        var files = Files(File("Users.sql"), File("Orders.sql"));

        var (placement, _) = Analyse(TestModels.Type(), files, new SettingsLevel { NestedClass = "Queries" });

        placement.EmptyNestedClass.ShouldBe("Queries");
        placement.NestedClasses.ShouldBeEmpty();
    }

    [Fact]
    public void Of_NoUsableQueryAndTheNameTaken_WritesNoEmptyNestedClass()
    {
        var (placement, _) = Analyse(TestModels.Type(takenNames: ["Sql", "UserRepository"]), Files(File("Users.sql")));

        placement.EmptyNestedClass.ShouldBeNull();
    }

    // A type whose usable queries are all methods writes no nested class, so that a method may take its name.
    [Fact]
    public void Of_OnlyMethods_WritesNoEmptyNestedClass()
    {
        var (placement, _) = Analyse(TestModels.Type(), Files(File("Users.sql", Query("Sql"))));

        placement.EmptyNestedClass.ShouldBeNull();
        placement.NestedClasses.ShouldBeEmpty();
    }

    // The methods' planner runs the analysis without diagnostics, and must see the same files.
    [Fact]
    public void Of_WithoutDiagnostics_LeavesOutTheSameFiles()
    {
        var files = Files(
            File("Users.sql", Query("GetUser", output: OutputKind.Sql, visibility: Visibility.Public)),
            File(
                "Orders.sql",
                Query("GetOrder", output: OutputKind.Sql, visibility: Visibility.Private, at: At("Orders.sql", 0))
            )
        );

        var placement = TypePlacement.Of(TestModels.Type(), files, Levels(files.Count, SettingsLevel.None), null);

        placement.Usable.ShouldBe([false, false]);
    }

    [Fact]
    public void DefaultVisibility_OfEachLocation_NeverFails()
    {
        PlacementChecks.DefaultVisibility(MemberLocation.NestedClass, true).ShouldBe(Visibility.Private);
        PlacementChecks.DefaultVisibility(MemberLocation.Class, true).ShouldBe(Visibility.Public);
        PlacementChecks.DefaultVisibility(MemberLocation.Class, false).ShouldBe(Visibility.Internal);
        PlacementChecks.DefaultVisibility(MemberLocation.ExtensionClass, true).ShouldBe(Visibility.Internal);
        PlacementChecks.DefaultVisibility(MemberLocation.ExtensionClass, false).ShouldBe(Visibility.Internal);
        foreach (
            var location in (MemberLocation[])
                [MemberLocation.NestedClass, MemberLocation.Class, MemberLocation.ExtensionClass]
        )
        {
            foreach (var isPublic in (bool[])[true, false])
            {
                var type = TestModels.Type(isPublic: isPublic, isReachableAsProtected: !isPublic);
                PlacementChecks
                    .MethodModelsReason(type, location, PlacementChecks.DefaultVisibility(location, isPublic))
                    .ShouldBeNull();
            }
        }
    }

    [Fact]
    public void PlaceText_OfEachPlace_NamesIt()
    {
        PlaceText.Of(SettingPlace.Query).ShouldBe("its own marker");
        PlaceText.Of(SettingPlace.FileHeader).ShouldBe("the file header");
        PlaceText.Of(SettingsFile).ShouldBe("the settings file '/app/sqlsource.json'");
    }

    // A type nested as protected in a public type: not public, and reachable from a derived type of another assembly.
    private static TargetType ReachableAsProtected() => TestModels.Type(isPublic: false, isReachableAsProtected: true);

    private static SettingPlace SettingsFile => SettingPlace.OfSettingsFile(SettingsPath, 1);

    private static (TypePlacement Placement, ImmutableArray<DiagnosticInfo> Diagnostics) Analyse(
        TargetType type,
        EquatableArray<ParsedSqlFile> files,
        SettingsLevel? level = null
    )
    {
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var placement = TypePlacement.Of(type, files, Levels(files.Count, level ?? SettingsLevel.None), diagnostics);
        return (placement, diagnostics.ToImmutable());
    }

    private static QueryPlacement PlacementOf(TypePlacement placement, int file, int query) =>
        placement.PlacementOf(placement.SettingsOf(file, query));

    private static EquatableArray<SettingsLevel> Levels(int count, SettingsLevel level)
    {
        var levels = new SettingsLevel[count];
        System.Array.Fill(levels, level);
        return TestSidecars.Of(levels);
    }

    private static EquatableArray<ParsedSqlFile> Files(params ParsedSqlFile[] files) => TestSidecars.Of(files);

    private static ParsedSqlFile File(string fileName, params SqlQuery[] queries) =>
        new(
            "/app/Repo/" + fileName,
            fileName,
            TestSidecars.Of(queries),
            TestSidecars.Of<DiagnosticInfo>(),
            TestSidecars.Of<DiagnosticInfo>(),
            SqlDialect.Ansi
        );

    // A query whose markers give what the test writes, from the file header unless the place says otherwise, at the
    // first line of Users.sql unless the test gives another place.  Its output is Methods unless the test says.
    private static SqlQuery Query(
        string name,
        OutputKind output = OutputKind.Methods,
        MemberLocation? location = null,
        Visibility? visibility = null,
        string? nestedClass = null,
        GeneratorParameters parameters = GeneratorParameters.None,
        SettingPlace? place = null,
        LocationInfo? at = null
    ) =>
        TestModels.Query(
            name,
            "SELECT 1;",
            nameLocation: at ?? At("Users.sql", 0),
            markers: new SettingsLevel
            {
                Output = output,
                Parameters = TestModels.TurnedOn(parameters),
                MemberLocation = location is { } written
                    ? new Placed<MemberLocation>(written, place ?? SettingPlace.FileHeader)
                    : null,
                Visibility = visibility is { } value
                    ? new Placed<Visibility>(value, place ?? SettingPlace.FileHeader)
                    : null,
                NestedClass = nestedClass,
            }
        );

    // The start of a line of the file.
    private static LocationInfo At(string file, int line) =>
        new(
            "/app/Repo/" + file,
            new TextSpan(line * 20, 5),
            new LinePositionSpan(new LinePosition(line, 0), new LinePosition(line, 5))
        );
}
