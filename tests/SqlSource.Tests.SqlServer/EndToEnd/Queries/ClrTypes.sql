-- The queries of SQL Server's CLR types, hierarchyid, geography and geometry, claimed by ClrTypeQueries.  The type
-- override of the file header reads and sends each as the type of the package Microsoft.SqlServer.Types that the
-- manual names for it.  The rows are those of a table variable, so the queries need no table of the schema.
-- type-override: hierarchyid = Microsoft.SqlServer.Types.SqlHierarchyId
-- type-override: geography = Microsoft.SqlServer.Types.SqlGeography
-- type-override: geometry = Microsoft.SqlServer.Types.SqlGeometry

-- name: WriteClrTypes -> one
-- summary: Writes a hierarchyid, a geography and a geometry, each of which can be null, and reads them back.
-- param: @node null
-- param: @place null
-- param: @shape null
DECLARE @written TABLE (node hierarchyid NULL, place geography NULL, shape geometry NULL);
INSERT INTO @written (node, place, shape) VALUES (@node, @place, @shape);
SELECT node, place, shape FROM @written;

-- name: ListDescendants
-- summary: The nodes of a tree that descend from a node, the node itself among them, in the order of the tree.
DECLARE @tree TABLE (node hierarchyid NOT NULL);
INSERT INTO @tree (node) VALUES ('/'), ('/1/'), ('/1/1/'), ('/1/2/'), ('/2/');
SELECT node FROM @tree WHERE node.IsDescendantOf(@ancestor) = 1 ORDER BY node;
