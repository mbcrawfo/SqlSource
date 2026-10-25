using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Generation.Emitting;
using SqlSource.Generation.Methods;
using SqlSource.Parsing.Tokens;
using SqlSource.Settings;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generation.Emitting;

public sealed class QueryMethodWriterTests
{
    private const string Vector = "global::Microsoft.Data.SqlTypes.SqlVector<float>";

    private const string ReaderLine =
        "var reader = await command.ExecuteReaderAsync("
        + "global::System.Data.CommandBehavior.Default, cancellationToken).ConfigureAwait(false);\n";

    // Ends a line of an expected text that goes on in the next, written so to keep within the width of a line.
    private const string Continued = "\u21a9";

    // GetUser -> one-optional of the spec: one parameter, one token, a record of two columns.
    private static readonly QueryMethod GetUser = new(
        "GetUser",
        "GetUser",
        "The <c>GetUser</c> query from <c>Users.sql</c>.",
        ResultShape.OneOptional,
        CollectionKind.List,
        DatabaseEngine.PostgreSql,
        TestModels.Array(TestModels.Literal("SELECT id, name FROM users WHERE id = @id ORDER BY "), Token("orderBy")),
        true,
        new MethodInput("global::App.GetUserParams", TestModels.Array(new MethodParameter("id", "Id", true, false))),
        new MethodOutput(
            "global::App.GetUserDto",
            true,
            TestModels.Array(
                new MethodColumn("id", "Id", "int", false),
                new MethodColumn("name", "Name", "string", true)
            ),
            TestModels.Array(0, 1)
        ),
        TestModels.NameLocation("Users.sql")
    );

    [Fact]
    public void Append_OneOptionalWithParametersAndAToken_IsTheMethodOfTheSpec()
    {
        var builder = new StringBuilder("before\n");

        QueryMethodWriter.Append(builder, "    ", GetUser, "public static", isExtension: true);

        builder
            .ToString()
            .ShouldBe(
                Joined(
                    """
                    before
                        /// <summary>
                        /// The <c>GetUser</c> query from <c>Users.sql</c>.
                        /// </summary>
                        /// <remarks>
                        /// <code>
                        /// SELECT id, name FROM users WHERE id = @id ORDER BY {{orderBy}}
                        /// </code>
                        /// </remarks>
                        /// <param name="connection">The connection to run the query on.  ↩
                    It is opened when it is closed, and closed again when the method opened it.</param>
                        /// <param name="parameters">The query's parameters.</param>
                        /// <param name="orderBy">The text that replaces <c>{{orderBy}}</c>.</param>
                        /// <param name="transaction">The transaction to run the query in, or null.</param>
                        /// <param name="commandTimeout">↩
                    The command's timeout in seconds, or null for the driver's.</param>
                        /// <param name="cancellationToken">Cancels the query.</param>
                        /// <returns>The row, or null when there is none.</returns>
                        /// <exception cref="global::SqlSource.UnexpectedRowCountException">↩
                    The query returned more than one row.</exception>
                        public static async global::System.Threading.Tasks.Task<global::App.GetUserDto?> GetUser(
                            this global::System.Data.Common.DbConnection connection,
                            global::App.GetUserParams parameters,
                            string orderBy,
                            global::System.Data.Common.DbTransaction? transaction = null,
                            int? commandTimeout = null,
                            global::System.Threading.CancellationToken cancellationToken = default)
                        {
                            global::System.ArgumentNullException.ThrowIfNull(connection);
                            global::System.ArgumentNullException.ThrowIfNull(parameters);
                            global::System.ArgumentException.ThrowIfNullOrWhiteSpace(orderBy);
                            var sql = string.Create(
                                checked(51 + orderBy.Length),
                                orderBy,
                                static (span, state) =>
                                {
                                    "SELECT id, name FROM users WHERE id = @id ORDER BY ".CopyTo(span);
                                    span = span.Slice(51);
                                    state.CopyTo(span);
                                }
                            );
                            var wasClosed = connection.State == global::System.Data.ConnectionState.Closed;
                            if (wasClosed)
                            {
                                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                            }

                            try
                            {
                                var command = connection.CreateCommand();
                                await using var commandScope = ↩
                    global::System.Threading.Tasks.TaskAsyncEnumerableExtensions.ConfigureAwait(command, false);
                                command.CommandText = sql;
                                command.Transaction = transaction;
                                if (commandTimeout.HasValue)
                                {
                                    command.CommandTimeout = commandTimeout.Value;
                                }

                                {
                                    var parameter = command.CreateParameter();
                                    parameter.ParameterName = "id";
                                    parameter.Value = parameters.Id;
                                    command.Parameters.Add(parameter);
                                }

                                var reader = await command.ExecuteReaderAsync(↩
                    global::System.Data.CommandBehavior.Default, cancellationToken).ConfigureAwait(false);
                                await using var readerScope = ↩
                    global::System.Threading.Tasks.TaskAsyncEnumerableExtensions.ConfigureAwait(reader, false);
                                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                                {
                                    return null;
                                }

                                var row = new global::App.GetUserDto(reader.GetFieldValue<int>(0), ↩
                    reader.IsDBNull(1) ? default(string?) : reader.GetFieldValue<string>(1));
                                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                                {
                                    throw new global::SqlSource.UnexpectedRowCountException("GetUser", true);
                                }

                                return row;
                            }
                            finally
                            {
                                if (wasClosed)
                                {
                                    await connection.CloseAsync().ConfigureAwait(false);
                                }
                            }
                        }

                    """
                )
            );
    }

    [Theory]
    [InlineData(
        nameof(ResultShape.Many),
        "global::System.Threading.Tasks.Task<global::System.Collections.Generic.List<global::App.GetUserDto>>",
        ReaderLine,
        "<returns>The rows the query returned.</returns>"
    )]
    [InlineData(
        nameof(ResultShape.One),
        "global::System.Threading.Tasks.Task<global::App.GetUserDto>",
        ReaderLine,
        "<returns>The row.</returns>"
    )]
    [InlineData(
        nameof(ResultShape.OneOptional),
        "global::System.Threading.Tasks.Task<global::App.GetUserDto?>",
        ReaderLine,
        "<returns>The row, or null when there is none.</returns>"
    )]
    [InlineData(
        nameof(ResultShape.None),
        "global::System.Threading.Tasks.Task",
        "        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);\n    }\n",
        "<returns>A task that completes when the query has run.</returns>"
    )]
    [InlineData(
        nameof(ResultShape.RowCount),
        "global::System.Threading.Tasks.Task<int>",
        "        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);\n    }\n",
        "<returns>The number of rows the query affected.</returns>"
    )]
    public void Append_EachShape_ReturnsAndExecutesAsTheShapeSays(
        string shapeName,
        string returns,
        string execute,
        string returnsXml
    )
    {
        var shape = Enum.Parse<ResultShape>(shapeName);

        var text = Write(GetUser with { Shape = shape });

        text.ShouldContain("public static async " + returns + " GetUser(\n");
        text.ShouldContain(execute);
        text.ShouldContain("/// " + returnsXml + "\n");
        var isNonQuery = shape is ResultShape.None or ResultShape.RowCount;
        text.Contains("ExecuteReaderAsync", StringComparison.Ordinal).ShouldBe(!isNonQuery);
        text.Contains("ExecuteNonQueryAsync", StringComparison.Ordinal).ShouldBe(isNonQuery);

        if (shape == ResultShape.One)
        {
            text.ShouldContain(
                "/// <exception cref=\"global::SqlSource.UnexpectedRowCountException\">"
                    + "The query returned no rows, or more than one.</exception>\n"
            );
            text.ShouldNotContain("return null;");
            text.ShouldContain(
                "            throw new global::SqlSource.UnexpectedRowCountException(\"GetUser\", false);\n"
            );
            text.ShouldContain(
                "            throw new global::SqlSource.UnexpectedRowCountException(\"GetUser\", true);\n"
            );
        }
        else if (shape == ResultShape.OneOptional)
        {
            text.ShouldContain(
                "/// <exception cref=\"global::SqlSource.UnexpectedRowCountException\">"
                    + "The query returned more than one row.</exception>\n"
            );
            text.ShouldContain("            return null;\n");
            text.ShouldNotContain("UnexpectedRowCountException(\"GetUser\", false)");
            text.ShouldContain(
                "            throw new global::SqlSource.UnexpectedRowCountException(\"GetUser\", true);\n"
            );
        }
        else
        {
            text.ShouldNotContain("UnexpectedRowCountException");
            text.ShouldNotContain("<exception");
        }
    }

    [Theory]
    [InlineData(nameof(CollectionKind.Array), "global::App.GetUserDto[]", "List", "rows.ToArray()")]
    [InlineData(
        nameof(CollectionKind.IEnumerable),
        "global::System.Collections.Generic.IEnumerable<global::App.GetUserDto>",
        "List",
        "rows.ToArray()"
    )]
    [InlineData(
        nameof(CollectionKind.IReadOnlyCollection),
        "global::System.Collections.Generic.IReadOnlyCollection<global::App.GetUserDto>",
        "List",
        "rows.ToArray()"
    )]
    [InlineData(
        nameof(CollectionKind.IReadOnlyList),
        "global::System.Collections.Generic.IReadOnlyList<global::App.GetUserDto>",
        "List",
        "rows.ToArray()"
    )]
    [InlineData(
        nameof(CollectionKind.List),
        "global::System.Collections.Generic.List<global::App.GetUserDto>",
        "List",
        "rows"
    )]
    [InlineData(
        nameof(CollectionKind.IList),
        "global::System.Collections.Generic.IList<global::App.GetUserDto>",
        "List",
        "rows"
    )]
    [InlineData(
        nameof(CollectionKind.ICollection),
        "global::System.Collections.Generic.ICollection<global::App.GetUserDto>",
        "List",
        "rows"
    )]
    [InlineData(
        nameof(CollectionKind.ImmutableArray),
        "global::System.Collections.Immutable.ImmutableArray<global::App.GetUserDto>",
        "ImmutableArray",
        "rows.DrainToImmutable()"
    )]
    [InlineData(
        nameof(CollectionKind.ImmutableList),
        "global::System.Collections.Immutable.ImmutableList<global::App.GetUserDto>",
        "ImmutableList",
        "rows.ToImmutable()"
    )]
    [InlineData(
        nameof(CollectionKind.IImmutableList),
        "global::System.Collections.Immutable.IImmutableList<global::App.GetUserDto>",
        "ImmutableList",
        "rows.ToImmutable()"
    )]
    public void Append_EachCollectionType_BuffersAndMaterialisesAsDecided(
        string collectionName,
        string returns,
        string buffer,
        string result
    )
    {
        var text = Write(
            GetUser with
            {
                Shape = ResultShape.Many,
                Collection = Enum.Parse<CollectionKind>(collectionName),
            }
        );

        text.ShouldContain("public static async global::System.Threading.Tasks.Task<" + returns + "> GetUser(\n");
        text.ShouldContain(
            "        var rows = "
                + (
                    buffer == "List"
                        ? "new global::System.Collections.Generic.List<global::App.GetUserDto>()"
                        : "global::System.Collections.Immutable." + buffer + ".CreateBuilder<global::App.GetUserDto>()"
                )
                + ";\n"
                + "        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))\n"
                + "        {\n"
                + "            rows.Add(new global::App.GetUserDto(reader.GetFieldValue<int>(0), "
                + "reader.IsDBNull(1) ? default(string?) : reader.GetFieldValue<string>(1)));\n"
                + "        }\n"
                + "\n"
                + "        return "
                + result
                + ";\n"
                + "    }\n"
                + "    finally\n"
        );
    }

    [Theory]
    [InlineData("public static")]
    [InlineData("internal static")]
    [InlineData("private static")]
    public void Append_TypePlacement_TakesTheConnectionWithoutThis(string modifiers)
    {
        var text = Write(GetUser, modifiers, isExtension: false);

        text.ShouldContain(
            modifiers
                + " async global::System.Threading.Tasks.Task<global::App.GetUserDto?> GetUser(\n"
                + "    global::System.Data.Common.DbConnection connection,\n"
        );
        text.ShouldNotContain("this ");
    }

    [Fact]
    public void Append_NoParameters_HasNoParametersArgumentNorBinding()
    {
        var text = Write(GetUser with { Input = null });

        text.ShouldNotContain("parameters");
        text.ShouldNotContain("CreateParameter");
        text.ShouldContain("    this global::System.Data.Common.DbConnection connection,\n    string orderBy,\n");
        text.ShouldContain("        }\n\n        var reader = await command.ExecuteReaderAsync(", Case.Sensitive);
    }

    [Fact]
    public void Append_NoTokens_HoldsTheSqlInAConstant()
    {
        var text = Write(
            GetUser with
            {
                Segments = TestModels.Array(TestModels.Literal("SELECT id, name\nFROM users;")),
            }
        );

        text.ShouldContain("/// <code>\n/// SELECT id, name\n/// FROM users;\n/// </code>\n");
        text.ShouldNotContain("<param name=\"orderBy\"");
        text.ShouldContain(
            "    global::App.GetUserParams parameters,\n"
                + "    global::System.Data.Common.DbTransaction? transaction = null,\n"
        );
        text.ShouldContain(
            "    global::System.ArgumentNullException.ThrowIfNull(parameters);\n"
                + "    const string sql = \"SELECT id, name\\nFROM users;\";\n"
                + "    var wasClosed = "
        );
        text.ShouldNotContain("ThrowIfNullOrWhiteSpace");
        text.ShouldNotContain("string.Create");
    }

    [Fact]
    public void Append_NoTokenValidation_DoesNotCheckTheTokens()
    {
        var validated = Write(GetUser);
        var unvalidated = Write(GetUser with { ValidateTokens = false });

        unvalidated.ShouldNotContain("ThrowIfNullOrWhiteSpace");
        validated.ShouldBe(
            unvalidated.Replace(
                "ThrowIfNull(parameters);\n",
                "ThrowIfNull(parameters);\n    global::System.ArgumentException.ThrowIfNullOrWhiteSpace(orderBy);\n",
                StringComparison.Ordinal
            )
        );
    }

    [Fact]
    public void Append_RecordOutput_ReadsByThePositionalConstructor() =>
        Write(GetUser)
            .ShouldContain(
                "        var row = new global::App.GetUserDto(reader.GetFieldValue<int>(0), "
                    + "reader.IsDBNull(1) ? default(string?) : reader.GetFieldValue<string>(1));\n"
            );

    [Fact]
    public void Append_ClassOutput_ReadsByAnInitializer() =>
        Write(GetUser with { Output = GetUser.Output! with { IsRecord = false } })
            .ShouldContain(
                "        var row = new global::App.GetUserDto { Id = reader.GetFieldValue<int>(0), "
                    + "Name = reader.IsDBNull(1) ? default(string?) : reader.GetFieldValue<string>(1) };\n"
            );

    // Sorted, the model's properties are Email, Id, Name, and the constructor takes the columns in that order.
    [Fact]
    public void Append_SortedRecordOutput_PassesEachColumnToItsProperty() =>
        Write(
                GetUser with
                {
                    Output = new MethodOutput(
                        "global::App.GetUserDto",
                        true,
                        TestModels.Array(
                            new MethodColumn("name", "Name", "string", false),
                            new MethodColumn("id", "Id", "int", false),
                            new MethodColumn("email", "Email", "string", true)
                        ),
                        TestModels.Array(2, 1, 0)
                    ),
                }
            )
            .ShouldContain(
                "        var row = new global::App.GetUserDto("
                    + "reader.IsDBNull(2) ? default(string?) : reader.GetFieldValue<string>(2), "
                    + "reader.GetFieldValue<int>(1), "
                    + "reader.GetFieldValue<string>(0));\n"
            );

    [Fact]
    public void Append_NullableAndNotNullColumns_ReadWithIsDBNullOnlyWhenNullable()
    {
        var text = Write(
            GetUser with
            {
                Output = GetUser.Output! with
                {
                    IsRecord = false,
                    Columns = TestModels.Array(
                        new MethodColumn("a", "A", "int", false),
                        new MethodColumn("b", "B", "int", true),
                        new MethodColumn("c", "C", "string", false),
                        new MethodColumn("d", "D", "string", true)
                    ),
                    ConstructorOrder = TestModels.Array(0, 1, 2, 3),
                },
            }
        );

        text.ShouldContain(
            "{ A = reader.GetFieldValue<int>(0), "
                + "B = reader.IsDBNull(1) ? default(int?) : reader.GetFieldValue<int>(1), "
                + "C = reader.GetFieldValue<string>(2), "
                + "D = reader.IsDBNull(3) ? default(string?) : reader.GetFieldValue<string>(3) };\n"
        );
        text.ShouldNotContain("GetFieldValue<int?>");
        text.ShouldNotContain("GetFieldValue<string?>");
    }

    [Fact]
    public void Append_NotNullVectorColumn_ChecksIsNullAndThrowsWithTheColumnsName() =>
        Write(GetUser with { Output = VectorOutput(isNullable: false) })
            .ShouldContain(
                "        var value1 = reader.GetFieldValue<"
                    + Vector
                    + ">(1);\n"
                    + "        if (value1.IsNull)\n"
                    + "        {\n"
                    + "            throw new global::System.InvalidCastException(\"Column 'embedding' is null.\");\n"
                    + "        }\n"
                    + "\n"
                    + "        var row = new global::App.GetUserDto(reader.GetFieldValue<int>(0), value1);\n"
            );

    [Fact]
    public void Append_NullableVectorColumn_HasNoIsNullCheck()
    {
        var text = Write(GetUser with { Output = VectorOutput(isNullable: true) });

        text.ShouldNotContain("IsNull)");
        text.ShouldNotContain("InvalidCastException");
        text.ShouldContain(
            "        var row = new global::App.GetUserDto(reader.GetFieldValue<int>(0), "
                + "reader.IsDBNull(1) ? default("
                + Vector
                + "?) : reader.GetFieldValue<"
                + Vector
                + ">(1));\n"
        );
    }

    [Fact]
    public void Append_NotNullReferenceParameter_SendsDbNullForNull() =>
        Write(
                GetUser with
                {
                    Input = new MethodInput(
                        "global::App.GetUserParams",
                        TestModels.Array(
                            new MethodParameter("id", "Id", true, false),
                            new MethodParameter("name", "Name", false, false)
                        )
                    ),
                }
            )
            .ShouldContain(
                "            parameter.Value = parameters.Id;\n"
                    + "            command.Parameters.Add(parameter);\n"
                    + "        }\n"
                    + "\n"
                    + "        {\n"
                    + "            var parameter = command.CreateParameter();\n"
                    + "            parameter.ParameterName = \"name\";\n"
                    + "            parameter.Value = (object?)parameters.Name ?? global::System.DBNull.Value;\n"
                    + "            command.Parameters.Add(parameter);\n"
                    + "        }\n"
                    + "\n"
            );

    [Fact]
    public void Append_TokenNamedLikeALocal_RenamesTheLocal()
    {
        var text = Write(
            GetUser with
            {
                Segments = TestModels.Array(TestModels.Literal("SELECT 1 FROM "), Token("sql"), Token("span")),
            }
        );

        text.ShouldContain("    var sql1 = string.Create(\n");
        text.ShouldContain("        static (span1, state) =>\n");
        text.ShouldContain("        command.CommandText = sql1;\n");
    }

    // An asynchronous method cannot use await as an identifier, so the argument is verbatim wherever the method uses
    // it; its documentation keeps the name.
    [Fact]
    public void Append_TokenNamedAwait_WritesItVerbatimWhereItIsAnIdentifier()
    {
        var text = Write(
            GetUser with
            {
                Segments = TestModels.Array(TestModels.Literal("SELECT 1 FROM "), Token("await"), Token("b")),
            }
        );

        text.ShouldContain("/// <param name=\"await\">The text that replaces <c>{{await}}</c>.</param>\n");
        text.ShouldContain("    string @await,\n    string b,\n");
        text.ShouldContain("    global::System.ArgumentException.ThrowIfNullOrWhiteSpace(@await);\n");
        text.ShouldContain("        checked(14 + @await.Length + b.Length),\n        (@await, b),\n");
        text.ShouldNotContain("string await");
        text.ShouldNotContain("(await)");
        text.ShouldNotContain(" await.Length");
        text.ShouldNotContain("(await, b)");
    }

    // A conversion by element reads the base map's array, with nullable elements when they are of a value type, and
    // converts each element; ReadText is the array it reads.
    [Theory]
    [InlineData(
        "string[]",
        false,
        "string[]",
        "global::System.Array.ConvertAll<string, global::App.Email>(reader.GetFieldValue<string[]>(0), "
    )]
    [InlineData(
        "long[]",
        true,
        "long?[]",
        "global::System.Array.ConvertAll<long?, global::App.Email>(reader.GetFieldValue<long?[]>(0), "
    )]
    public void Append_ColumnConvertedByElement_ReadsTheDatabasesArray(
        string databaseText,
        bool databaseIsValueType,
        string readText,
        string read
    )
    {
        var column = new MethodColumn(
            "emails",
            "Emails",
            "global::App.Email[]",
            false,
            new Conversion(
                "global::App.Email.FromDatabase",
                databaseText,
                databaseIsValueType,
                true,
                "global::App.Email",
                true
            )
        );

        var text = Write(
            GetUser with
            {
                Output = GetUser.Output! with
                {
                    Columns = TestModels.Array(column),
                    ConstructorOrder = TestModels.Array(0),
                },
            }
        );

        column.ReadText.ShouldBe(readText);
        text.ShouldContain(
            "new global::App.GetUserDto("
                + read
                + "static element => element is { } item ? global::App.Email.FromDatabase(item) : "
                + "throw new global::System.InvalidCastException(\"Column 'emails' holds a null element.\")))"
        );
    }

    // An element the property writes nullable keeps a NULL element as null, the converter not called for it.
    [Fact]
    public void Append_ColumnOfANullableElementConvertedByElement_KeepsANullElement()
    {
        var column = new MethodColumn(
            "ids",
            "Ids",
            "global::App.UserId?[]",
            false,
            new Conversion("global::App.UserId.FromDatabase", "long[]", true, true, "global::App.UserId?", true)
        );

        var text = Write(
            GetUser with
            {
                Output = GetUser.Output! with
                {
                    Columns = TestModels.Array(column),
                    ConstructorOrder = TestModels.Array(0),
                },
            }
        );

        column.ReadText.ShouldBe("long?[]");
        text.ShouldContain(
            "new global::App.GetUserDto(global::System.Array.ConvertAll<long?, global::App.UserId?>("
                + "reader.GetFieldValue<long?[]>(0), "
                + "static element => element is { } item ? global::App.UserId.FromDatabase(item) : null))"
        );
    }

    [Fact]
    public void Append_SummaryOfTheQuery_IsTheMethodsSummary() =>
        Write(GetUser with { SummaryXml = "Finds a user by <c>id</c>,\nor none." })
            .ShouldStartWith("/// <summary>\n/// Finds a user by <c>id</c>,\n/// or none.\n/// </summary>\n");

    // Not required of this writer's tests, which compare text: the driver tests compile it.  Every shape, collection
    // type and placement, with names that could hide what the method writes, compiled together against the real
    // System.Data and SqlClient's SqlVector.
    [Fact]
    public void Append_EveryShapeAndCollectionType_Compiles()
    {
        var awkward = TestModels.Array(
            TestModels.Literal("SELECT 1 FROM "),
            Token("sql"),
            Token("command"),
            Token("reader"),
            Token("row"),
            Token("rows"),
            Token("value2"),
            Token("span"),
            Token("state"),
            Token("wasClosed"),
            Token("parameter"),
            Token("global"),
            Token("var"),
            Token("System"),
            Token("App"),
            Token("await")
        );
        var withVector = GetUser.Output! with
        {
            Columns = TestModels.Array(
                new MethodColumn("id", "Id", "int", false),
                new MethodColumn("name", "Name", "string", true),
                new MethodColumn("embedding", "Embedding", Vector, false),
                new MethodColumn("other", "Other", Vector, true)
            ),
            ConstructorOrder = TestModels.Array(0, 1, 2, 3),
        };
        var asClass = withVector with { TypeText = "global::App.GetUserClass", IsRecord = false };
        var builder = new StringBuilder("namespace App\n{\n    internal static class Queries\n    {\n");
        var index = 0;
        foreach (var shape in Enum.GetValues<ResultShape>())
        {
            foreach (var collection in Enum.GetValues<CollectionKind>())
            {
                foreach (var output in new[] { withVector, asClass })
                {
                    var method = GetUser with
                    {
                        Name = "Q" + index++,
                        Shape = shape,
                        Collection = collection,
                        Output = output,
                        Segments = index % 2 == 0 ? awkward : GetUser.Segments,
                        Input = index % 3 == 0 ? null : GetUser.Input,
                    };
                    // Every awkward method once by each placement: the segments go by twos, the placement by fours.
                    QueryMethodWriter.Append(builder, "        ", method, "public static", isExtension: index % 4 < 2);
                }
            }
        }

        _ = builder.Append(
            """
                }

                internal sealed record GetUserParams(int Id);

                internal sealed record GetUserDto(
                    int Id,
                    string? Name,
                    global::Microsoft.Data.SqlTypes.SqlVector<float> Embedding,
                    global::Microsoft.Data.SqlTypes.SqlVector<float>? Other
                );

                internal sealed class GetUserClass
                {
                    public int Id { get; init; }

                    public string? Name { get; init; }

                    public global::Microsoft.Data.SqlTypes.SqlVector<float> Embedding { get; init; }

                    public global::Microsoft.Data.SqlTypes.SqlVector<float>? Other { get; init; }
                }
            }

            namespace SqlSource
            {
                internal sealed class UnexpectedRowCountException : global::System.InvalidOperationException
                {
                    public UnexpectedRowCountException(string queryName, bool moreThanOne) { }
                }
            }

            """
        );

        var compilation = GeneratorHarness.CreateCompilation(
            [new SourceFile("/app/Queries.cs", "#nullable enable\n" + builder)],
            references: [SqlClientReferences.SqlClient7]
        );

        compilation
            .GetDiagnostics(TestContext.Current.CancellationToken)
            // CS1701, SqlClient's System.Data.Common unified with the runtime's, has no place in the source.
            .Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning && diagnostic.Location.IsInSource)
            .Select(diagnostic => diagnostic.ToString())
            .ShouldBeEmpty();
    }

    private static MethodOutput VectorOutput(bool isNullable) =>
        GetUser.Output! with
        {
            Columns = TestModels.Array(
                new MethodColumn("id", "Id", "int", false),
                new MethodColumn("embedding", "Embedding", Vector, isNullable)
            ),
        };

    // The text with each line that ends in the mark joined to the next, whose indentation is dropped.
    private static string Joined(string text) =>
        Regex.Replace(text, Continued + "\n *", string.Empty, RegexOptions.None, TimeSpan.FromSeconds(1));

    private static SqlSegment Token(string name) => new(SqlSegmentKind.Token, name);

    private static string Write(QueryMethod method, string modifiers = "public static", bool isExtension = true)
    {
        var builder = new StringBuilder();
        QueryMethodWriter.Append(builder, string.Empty, method, modifiers, isExtension);
        return builder.ToString();
    }
}
