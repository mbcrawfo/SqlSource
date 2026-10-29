; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SQLSRC001 | SqlSource | Error | Type must be partial
SQLSRC002 | SqlSource | Error | Type is file-local
SQLSRC003 | SqlSource | Error | Target framework is not supported
SQLSRC004 | SqlSource | Error | Path matches no SQL file
SQLSRC005 | SqlSource | Error | Folder has no SQL file
SQLSRC006 | SqlSource | Error | Attribute value is not valid
SQLSRC007 | SqlSource | Error | Type has a member named like its nested class
SQLSRC008 | SqlSource | Error | Query name is used in two files
SQLSRC009 | SqlSource | Error | Query is named like its containing type
SQLSRC012 | SqlSource | Error | Language version is not supported
SQLSRC013 | SqlSource | Error | SQL file paths differ only by case
SQLSRC015 | SqlSource | Error | Settings file is not a JSON object
SQLSRC016 | SqlSource | Error | Settings file key is not known
SQLSRC017 | SqlSource | Error | Settings file key is written twice
SQLSRC018 | SqlSource | Error | Settings file value has the wrong type
SQLSRC019 | SqlSource | Error | Settings file value is not valid
SQLSRC020 | SqlSource | Error | Settings file entry repeats a key
SQLSRC101 | SqlSource | Error | Quote is not closed
SQLSRC102 | SqlSource | Error | Comment is not closed
SQLSRC103 | SqlSource | Error | Query name is not valid
SQLSRC104 | SqlSource | Error | Query name is used twice
SQLSRC105 | SqlSource | Error | File name is not a valid query name
SQLSRC106 | SqlSource | Error | SQL before the first name
SQLSRC107 | SqlSource | Error | Summary before the first name
SQLSRC108 | SqlSource | Error | Marker has no SQL after it
SQLSRC109 | SqlSource | Error | Generator parameter is not known
SQLSRC110 | SqlSource | Error | Generator parameter is missing
SQLSRC111 | SqlSource | Error | Marker value is not valid
SQLSRC112 | SqlSource | Error | Settings conflict
SQLSRC113 | SqlSource | Error | Query has no SQL
SQLSRC114 | SqlSource | Error | Token name is a keyword
SQLSRC115 | SqlSource | Error | Dialect marker is misplaced
SQLSRC116 | SqlSource | Error | Marker is not allowed here
SQLSRC117 | SqlSource | Error | Parameter has no type
SQLSRC118 | SqlSource | Error | Parameter is not declared
SQLSRC119 | SqlSource | Error | Query has no parameters
SQLSRC120 | SqlSource | Error | Token crosses a comment
SQLSRC200 | SqlSource | Error | The tool failed unexpectedly
SQLSRC201 | SqlSource | Error | No project or solution found
SQLSRC202 | SqlSource | Error | More than one project or solution found
SQLSRC203 | SqlSource | Error | Path is not a project or a solution
SQLSRC204 | SqlSource | Error | Project does not use SqlSource
SQLSRC205 | SqlSource | Error | Project could not be evaluated
SQLSRC206 | SqlSource | Error | Project manifest cannot be read
SQLSRC207 | SqlSource | Error | Project is not in the run
SQLSRC208 | SqlSource | Error | Attribute argument is not a literal
SQLSRC209 | SqlSource | Error | Output needs a dialect that can be described
SQLSRC210 | SqlSource | Error | Token has no default
SQLSRC211 | SqlSource | Error | Database has two dialects
SQLSRC212 | SqlSource | Error | File is not in the run
SQLSRC213 | SqlSource | Error | Database has no connection
SQLSRC214 | SqlSource | Error | Two databases share a connection variable
SQLSRC215 | SqlSource | Error | Connection names no database
SQLSRC216 | SqlSource | Error | No describer for the dialect
SQLSRC217 | SqlSource | Error | Sidecar was not written
SQLSRC218 | SqlSource | Error | File could not be changed
SQLSRC219 | SqlSource | Error | Sidecar is out of date
SQLSRC220 | SqlSource | Error | Project was not restored
SQLSRC221 | SqlSource | Error | Sidecar was written by a newer tool
SQLSRC222 | SqlSource | Error | Directory cannot be read
SQLSRC223 | SqlSource | Error | Solution cannot be read
SQLSRC224 | SqlSource | Error | Database could not be opened
SQLSRC225 | SqlSource | Error | Server rejected the query
SQLSRC226 | SqlSource | Error | Parameter type could not be determined
SQLSRC227 | SqlSource | Error | Parameter's first use does not fix its type
SQLSRC228 | SqlSource | Error | Declared type is not valid
SQLSRC229 | SqlSource | Error | Column type is not supported
SQLSRC230 | SqlSource | Error | Query holds more than one statement
SQLSRC231 | SqlSource | Error | Parameters are read differently
SQLSRC232 | SqlSource | Error | Type cannot be bound by Npgsql
SQLSRC233 | SqlSource | Error | Statement cannot be described
SQLSRC234 | SqlSource | Error | Query writes a positional parameter
SQLSRC235 | SqlSource | Error | Server version is not supported
SQLSRC236 | SqlSource | Error | Connection does not keep a session
SQLSRC237 | SqlSource | Error | Sidecar cannot be read
SQLSRC238 | SqlSource | Error | Sidecar has a newer format
SQLSRC239 | SqlSource | Error | Sidecar has an older format
SQLSRC240 | SqlSource | Warning | Sidecar was written by another version of the tool
SQLSRC241 | SqlSource | Error | Query has no entry in the sidecar
SQLSRC242 | SqlSource | Error | Entry is of another database
SQLSRC243 | SqlSource | Error | No type map for the dialect
SQLSRC244 | SqlSource | Error | Entry is stale
SQLSRC245 | SqlSource | Warning | Entry names no query
SQLSRC246 | SqlSource | Error | Type has no C# type
SQLSRC247 | SqlSource | Error | Npgsql is older than 8.0
SQLSRC248 | SqlSource | Info | Npgsql is newer than SqlSource knows
SQLSRC249 | SqlSource | Error | Type needs Npgsql
SQLSRC250 | SqlSource | Error | Mapped type is not found
SQLSRC251 | SqlSource | Error | Mapped type is not accessible
SQLSRC252 | SqlSource | Error | Mapped type cannot be a property's type
SQLSRC253 | SqlSource | Error | Column has no name
SQLSRC254 | SqlSource | Error | Name is not an identifier after PascalCase
SQLSRC255 | SqlSource | Error | Two names give one property name
SQLSRC256 | SqlSource | Error | Property name is taken
SQLSRC257 | SqlSource | Error | Two models of one name differ
SQLSRC258 | SqlSource | Error | Model name cannot be used
SQLSRC259 | SqlSource | Error | Output model on a query without rows
SQLSRC260 | SqlSource | Error | Column marker names no usable column
SQLSRC261 | SqlSource | Error | Entry names an array as the catalog does
SQLSRC262 | SqlSource | Error | Copies of a parameter have different types
SQLSRC263 | SqlSource | Error | Query returns rows of different shapes
SQLSRC264 | SqlSource | Error | Query runs SQL the server cannot read
SQLSRC265 | SqlSource | Error | Query uses a temporary table in a batch
SQLSRC266 | SqlSource | Error | Column has no name
SQLSRC267 | SqlSource | Error | Microsoft.Data.SqlClient is older than 6.1
SQLSRC268 | SqlSource | Info | Microsoft.Data.SqlClient is newer than SqlSource knows
SQLSRC269 | SqlSource | Error | Type needs Microsoft.Data.SqlClient
SQLSRC270 | SqlSource | Error | Type needs a newer Microsoft.Data.SqlClient
SQLSRC271 | SqlSource | Error | File is not saved as UTF-8
SQLSRC272 | SqlSource | Error | Result shape needs rows
SQLSRC273 | SqlSource | Error | Driver is not referenced
SQLSRC274 | SqlSource | Error | Method name is taken
SQLSRC275 | SqlSource | Error | Token is named as an argument of the method
SQLSRC276 | SqlSource | Error | Extension class name is taken
SQLSRC277 | SqlSource | Error | Method marked as a converter is not one
SQLSRC278 | SqlSource | Error | Two converters convert between the same types
SQLSRC279 | SqlSource | Error | Converters do not convert from the database type
SQLSRC280 | SqlSource | Error | Converter is missing for one direction
SQLSRC284 | SqlSource | Warning | Short name finds a type that only a package brings
