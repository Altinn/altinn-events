-- Claims a single registered event for processing by a polling background
-- service task. Uses FOR UPDATE SKIP LOCKED so multiple concurrent poller
-- tasks can each claim a different event without blocking each other.
-- The claimed row remains locked until the caller commits or rolls back the
-- enclosing transaction (see UnitOfWork), at which point its status is
-- updated to 'processed' or left as 'registered' for retry.
CREATE OR REPLACE FUNCTION events.claim_registered_event()
    RETURNS TABLE(sequenceno bigint, cloudevent jsonb)
    LANGUAGE 'plpgsql'
AS $BODY$
BEGIN
    RETURN QUERY
    SELECT e.sequenceno, e.cloudevent
    FROM events.events e
    WHERE e.status = 'registered'
    ORDER BY e.sequenceno
    LIMIT 1
    FOR UPDATE SKIP LOCKED;
END;
$BODY$;

GRANT EXECUTE ON FUNCTION events.claim_registered_event() TO platform_events;