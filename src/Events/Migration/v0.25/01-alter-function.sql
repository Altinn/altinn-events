CREATE OR REPLACE FUNCTION events.getevents(
	_subject character varying,
	_after character varying,
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
	SELECT events.events.cloudevent
	FROM events.events
	WHERE (_subject = '' OR events.subject = _subject)
	AND (_type IS NULL OR events.type ILIKE ANY(_type) )
	AND (_source IS NULL OR events.source ILIKE ANY(_source))
	AND (_after = '' OR events.sequenceno >(
		SELECT
			case count(*)
			when 0
				then 0
			else
				(SELECT sequenceno
				FROM events.events
				WHERE id = _after
				ORDER BY sequenceno ASC
				LIMIT 1)
			end
		FROM events.events
		WHERE id = _after))
  ORDER BY events.sequenceno
  limit _size;
END;
$BODY$;
