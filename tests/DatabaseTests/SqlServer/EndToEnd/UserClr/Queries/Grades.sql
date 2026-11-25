-- The queries of a CLR type of a user's own assembly, dbo.Grade, claimed by UserClrQueries.  The type override of the
-- folder's settings file reads and sends it as its .NET build.

-- name: ListGrades
-- summary: Every grade, with its maybe, which can be null, in the order of the ids.
SELECT
    id,
    grade,
    maybe
FROM dbo.grades
ORDER BY id;

-- name: FindByGrade
-- summary: The ids of the rows of a grade, compared by the server.
SELECT id FROM dbo.grades
WHERE grade = @grade
ORDER BY id;

-- name: AddGrade
-- summary: Writes a grade and a maybe, which can be null.
-- param: @maybe null
INSERT INTO dbo.grades (id, grade, maybe) VALUES (@id, @grade, @maybe);

-- name: ListViewGrades
-- summary: The grades through the view, which passes the column on.
SELECT grade FROM dbo.grade_view
ORDER BY id;
