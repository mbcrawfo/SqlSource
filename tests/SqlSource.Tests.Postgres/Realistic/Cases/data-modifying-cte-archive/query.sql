-- name: ArchivePastReservations
WITH removed AS (
    DELETE FROM reservations r
    WHERE upper(r.during) < @cutoff
    RETURNING *
)
INSERT INTO events (id, account_id, at, kind, payload)
SELECT
    1000 + rm.id,
    rm.account_id,
    upper(rm.during),
    'reservation-archived',
    jsonb_build_object('reservation', rm.id, 'from', lower(rm.during), 'to', upper(rm.during))
FROM removed rm
RETURNING id, account_id, at, kind, payload;
