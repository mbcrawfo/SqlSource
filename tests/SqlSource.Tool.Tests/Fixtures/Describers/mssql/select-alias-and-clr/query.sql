-- name: TypedByEmail
SELECT id, dbo_email_value, hierarchyid_value
FROM dbo.all_types
WHERE dbo_email_value = @email AND hierarchyid_value = @node;
