-- The rows of the database "user_clr": two grades, the first with no maybe.  UserClrDatabase and
-- tools/describe-end-to-end.sh apply this after schema.sql, and a change to it is followed by a run of
-- tools/describe-end-to-end.sh.

INSERT INTO dbo.grades (id, grade, maybe) VALUES
(1, N'5', NULL),
(2, N'7', N'8');
