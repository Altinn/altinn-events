CREATE OR REPLACE FUNCTION events.getappevents_v2(
	_subject character varying,
	_after character varying,
	_from timestamp with time zone,
	_to timestamp with time zone,
	_type text[],
	_source text[],
	_resource text,
	_size integer)
    RETURNS TABLE(cloudevents text)
    LANGUAGE 'plpgsql'
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
		WHERE (_subject IS NULL OR cloudevent->>'subject' = _subject)
			AND (_from IS NULL OR cloudevent->>'time' >= _from::text)
			AND (_to IS NULL OR cloudevent->>'time' <= _to::text)
			AND registeredtime <= now() - interval '30 second'
			AND (_type IS NULL OR cloudevent->>'type' ILIKE ANY(_type))
			AND (_source IS NULL OR cloudevent->>'source' ILIKE ANY(_source))
			AND (_resource IS NULL OR cloudevent->>'resource' = _resource)
			AND (_after IS NULL OR _after = '' OR sequenceno > _sequenceno)
		ORDER BY sequenceno
		limit _size;
END;
$BODY$;
