CREATE OR REPLACE FUNCTION events.getappevent(
	_subject character varying,
	_after character varying,
	_from timestamp with time zone,
	_to timestamp with time zone,
	_type text[],
	_source text[],
	_size int)
    RETURNS TABLE(cloudevents text)
    LANGUAGE 'plpgsql'

AS $BODY$
BEGIN
return query
	SELECT events.events_app.cloudevent
	FROM events.events_app
	WHERE (_subject = '' OR events.subject = _subject)
	AND (_from IS NULL OR events.time >= _from)
	AND (_to IS NULL OR events.time <= _to)
	AND (_type IS NULL OR events.type ILIKE ANY(_type) )
	AND (_source IS NULL OR events.source ILIKE ANY(_source))
	AND (_after = '' OR events.sequenceno >(
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
  ORDER BY events.sequenceno
  limit _size;
END;
$BODY$;
