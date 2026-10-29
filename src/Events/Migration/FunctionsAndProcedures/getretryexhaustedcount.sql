CREATE OR REPLACE FUNCTION events.get_retry_exhausted_count()
    RETURNS bigint
    LANGUAGE 'sql'
AS $BODY$
    SELECT count(*)
    FROM events.events
    WHERE status = 3; -- RetryExhausted
$BODY$;
