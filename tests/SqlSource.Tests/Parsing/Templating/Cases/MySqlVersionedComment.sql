-- dialect: mysql, version=8.4.0
-- name: A
SELECT 1 /*!50700 , '*/' */, {{t:x}} /*!90000 a */ FROM t WHERE id = @id

-- name: B
SELECT /*! '*/' */ 2
