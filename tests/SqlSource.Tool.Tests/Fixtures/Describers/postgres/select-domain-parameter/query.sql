-- name: FindUserByEmailCast
SELECT id, name FROM users WHERE email = @email::email_t;
