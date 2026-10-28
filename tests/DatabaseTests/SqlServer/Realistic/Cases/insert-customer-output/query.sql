-- name: CreateCustomer
-- param: @name nvarchar(100)
-- param: @email nvarchar(254) null
-- param: @phone varchar(20) null
-- param: @company nvarchar(100) null
-- param: @time_zone varchar(50) null
INSERT INTO dbo.customers (name, email, phone, company, status, time_zone)
OUTPUT inserted.*
VALUES (@name, @email, @phone, @company, 'active', @time_zone);
