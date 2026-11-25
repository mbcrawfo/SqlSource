-- dialect: mariadb, version=11.4.2
-- name: A
SELECT 1 /*!50700 # c */
, 2 /*M!50700 , '*/' */ /*!100100 , '*/' */ FROM t WHERE id = @id
