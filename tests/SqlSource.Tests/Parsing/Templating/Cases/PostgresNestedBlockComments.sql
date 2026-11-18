-- dialect: postgres
-- name: Nested
SELECT /* a /* {{b:x}} */ {{c:y}} */ {{col:name}} FROM users;
