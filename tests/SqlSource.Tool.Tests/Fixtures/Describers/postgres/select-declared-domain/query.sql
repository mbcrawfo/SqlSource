-- name: FindUserByEmail
-- param: @email email_t
SELECT id, name FROM users WHERE email = @email;
