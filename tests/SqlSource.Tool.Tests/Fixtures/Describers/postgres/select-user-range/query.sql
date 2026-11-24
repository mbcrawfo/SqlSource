-- name: GetReadingSpans
SELECT r.id, r.span, r.spans
FROM readings AS r
WHERE r.span && @span;
