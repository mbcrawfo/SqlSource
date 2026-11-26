-- The schema of the database "user_clr", whose queries are of a CLR type of a user's own assembly.  The assembly,
-- the net48 build of tests/DatabaseTests/SqlServer.ClrType, is created in the database before this runs, by
-- UserClrDatabase and by tools/describe-end-to-end.sh, which apply this then seed.sql, batch by batch between the lines
-- that hold GO alone.  Every batch runs on SQL Server 2017.  A change to it is followed by a run of
-- tools/describe-end-to-end.sh, which writes the sidecars again.

-- The type of the assembly, by the name of the assembly and of its class.  sqlfluff's T-SQL reads no EXTERNAL NAME.
-- noqa: disable=PRS
CREATE TYPE dbo.Grade
EXTERNAL NAME [SqlSource.DatabaseTests.SqlServer.ClrType].[SqlSource.DatabaseTests.SqlServer.ClrType.Grade];
-- noqa: enable=PRS
GO

-- grade is never null and maybe can be.
CREATE TABLE dbo.grades (
    id int PRIMARY KEY,
    grade dbo.Grade NOT NULL,
    maybe dbo.Grade NULL
);
GO

-- A view that passes both columns on.
CREATE VIEW dbo.grade_view AS
SELECT
    id,
    grade,
    maybe
FROM dbo.grades;
GO
