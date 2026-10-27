-- The settings file of the project says postgres, which reads the second line as part of the escape string.
SELECT E'it'
    '\'s -- not a comment' AS note; -- a comment
