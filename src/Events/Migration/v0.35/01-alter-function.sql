CREATE OR REPLACE FUNCTION events.getevents(
	_resource character varying,
	_subject character varying,
	_alternativesubject character varying,
	_after character varying,
	_type text[],
	_size integer)
    RETURNS TABLE(cloudevents text) 
    LANGUAGE 'plpgsql'
    COST 100
    STABLE PARALLEL SAFE 
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
	WHERE  cloudevent->>'resource' = _resource
	AND (_subject IS NULL OR cloudevent->>'subject' = _subject)
	AND (_alternativeSubject IS NULL OR cloudevent->>'alternativesubject' = _alternativesubject)
	AND (_type IS NULL OR cloudevent->>'type' LIKE ANY(_type) )
	AND registeredtime <= now() - interval '30 second'
	AND (_after IS NULL OR _after = '' OR sequenceno > _sequenceno)
  ORDER BY sequenceno
  limit _size;
END;
$BODY$;