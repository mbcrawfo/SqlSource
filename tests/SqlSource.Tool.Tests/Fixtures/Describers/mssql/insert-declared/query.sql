-- name: AddUser
-- param: @nickname nvarchar(50)
-- param: @email dbo.Email
INSERT INTO dbo.users (name, email, nickname, credit) VALUES (@name, @email, @nickname, @credit);
