-- name: ListReadingsOfKinds
-- param: @kinds reading_kind_with_a_name_of_exactly_sixty_three_characters_long[]
SELECT r.id
FROM readings AS r
WHERE @kinds IS NOT NULL;
