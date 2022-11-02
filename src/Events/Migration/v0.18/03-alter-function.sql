CREATE OR REPLACE FUNCTION events.get(
	_subject character varying,
	_after character varying,
	_from timestamp with time zone,
	_to timestamp with time zone,
	_type text[],
	_source text[],
	_size integer)
    RETURNS TABLE(cloudevents text) 
    LANGUAGE 'plpgsql'
    COST 100
    VOLATILE PARALLEL UNSAFE
    ROWS 1000

AS $BODY$
BEGIN
return query
	SELECT events.events_app.cloudevent
	FROM events.events_app
	WHERE (_subject = '' OR events_app.subject = _subject)
	AND (_from IS NULL OR events_app.time >= _from)
	AND (_to IS NULL OR events_app.time <= _to)
	AND (_type IS NULL OR events_app.type ILIKE ANY(_type) )
	AND (_source IS NULL OR events_app.source ILIKE ANY(_source))
	AND (_after = '' OR events_app.sequenceno >(
		SELECT
			case count(*)
			when 0
				then 0
			else
				(SELECT sequenceno
				FROM events.events_app
				WHERE id = _after)
			end
		FROM events.events_app
		WHERE id = _after))
  ORDER BY events_app.sequenceno
  limit _size;
END;
$BODY$;


ALTER FUNCTION events.get(
	_subject character varying,
	_after character varying,
	_from timestamp with time zone,
	_to timestamp with time zone,
	_type text[],
	_source text[],
	_size integer)
RENAME TO getappevent;