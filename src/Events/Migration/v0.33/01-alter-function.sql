DROP FUNCTION IF EXISTS events.getappevents(character varying, character varying, timestamp with time zone, timestamp with time zone, text[], text[], integer);
CREATE OR REPLACE FUNCTION events.getappevents(
	_subject character varying,
	_after character varying,
	_from timestamp with time zone,
	_to timestamp with time zone,
	_type text[],
	_source text[],
	_size integer)
    RETURNS TABLE(cloudevents text)
    LANGUAGE 'plpgsql'
    --COST 100
    --VOLATILE PARALLEL UNSAFE
    ROWS 1000

AS $BODY$
DECLARE
_sequenceno bigint;
BEGIN
IF _after IS NOT NULL AND _after <> '' THEN
	SELECT
		case count(*)
		when 0
			then 0
		else
			(SELECT sequenceno FROM events.events
			WHERE cloudevent->>'id' = _after
			ORDER BY sequenceno ASC)
		end
	INTO _sequenceno
	FROM events.events
	WHERE cloudevent->>'id' = _after;
END IF;
return query
	SELECT cast(cloudevent as text) as cloudevents
		FROM events.events
		WHERE (_subject IS NULL OR _subject = '' OR cloudevent->>'subject' = _subject)
			AND (_from IS NULL OR cloudevent->>'time' >= _from::text)
			AND (_to IS NULL OR cloudevent->>'time' <= _to::text)
			AND registeredtime <= now() - interval '30 second'
			AND (_type IS NULL OR cloudevent->>'type' ILIKE ANY(_type))
			AND (_source IS NULL OR cloudevent->>'source' ILIKE ANY(_source))
			AND (_after IS NULL OR _after = '' OR sequenceno > _sequenceno)
		ORDER BY sequenceno
		limit _size;
END;
$BODY$;
