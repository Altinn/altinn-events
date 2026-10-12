CREATE OR REPLACE FUNCTION events.claim_registered_event()
    RETURNS TABLE(sequenceno bigint, cloudevent jsonb)
    LANGUAGE 'plpgsql'
AS $BODY$
BEGIN
    RETURN QUERY
    SELECT e.sequenceno, e.cloudevent
    FROM events.events e
    WHERE e.status = 1 -- Registered
    ORDER BY e.sequenceno
    LIMIT 1
    FOR UPDATE SKIP LOCKED;
END;
$BODY$;
