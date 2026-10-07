-- The project's dialect is postgres, which reads the third line as part of the escape string.
SELECT E'it' -- a comment between the parts
    '\'s -- not a comment' AS note; -- a comment
