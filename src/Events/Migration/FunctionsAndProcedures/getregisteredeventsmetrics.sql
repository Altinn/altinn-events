CREATE OR REPLACE FUNCTION events.get_registered_events_metrics()
    RETURNS TABLE(registeredcount bigint, oldestageseconds double precision)
    LANGUAGE 'sql'
AS $BODY$
    SELECT
        (SELECT count(*) FROM events.events WHERE status = 1), -- Registered
        COALESCE(
            (SELECT EXTRACT(EPOCH FROM now() - registeredtime)
             FROM events.events
             WHERE status = 1 -- Registered
             ORDER BY sequenceno
             LIMIT 1),
            0);
$BODY$;
