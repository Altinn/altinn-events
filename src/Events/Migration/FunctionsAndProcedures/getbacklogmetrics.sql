CREATE OR REPLACE FUNCTION events.get_backlog_metrics()
    RETURNS TABLE(registeredcount bigint, registeredoldestageseconds double precision, retryexhaustedcount bigint)
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
            0),
        (SELECT count(*) FROM events.events WHERE status = 3); -- RetryExhausted
$BODY$;
