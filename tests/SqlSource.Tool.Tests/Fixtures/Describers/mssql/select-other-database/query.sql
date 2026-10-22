-- name: JobsAndDatabases
SELECT j.job_id, d.database_id
FROM msdb.dbo.sysjobs AS j
CROSS JOIN master.sys.databases AS d;
