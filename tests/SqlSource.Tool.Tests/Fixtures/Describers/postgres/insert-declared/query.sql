-- name: AddUser
-- param: @name varchar(50)
-- param: @email email_t
INSERT INTO users (name, email, status) VALUES (@name, @email, @status);
