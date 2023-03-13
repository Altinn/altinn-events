CREATE OR REPLACE FUNCTION events.getevents(
	_subject character varying,
	_alternativesubject character varying,
	_after character varying,
	_type text[],
	_source character varying,	
	_size integer)
    RETURNS TABLE(cloudevents text)
    LANGUAGE 'plpgsql'
    COST 100
    VOLATILE PARALLEL UNSAFE
    ROWS 1000

AS $BODY$
BEGIN
return query
	SELECT cast(cloudevent as text) as cloudevents
	FROM events.events
	WHERE (_subject IS NULL OR cloudevent->>'subject' = _subject)
	AND (_alternativeSubject IS NULL OR cloudevent->>'alternativesubject' = _alternativesubject)
	AND (_source IS NULL OR cloudevent->>'source' ILIKE _source)
	AND (_type IS NULL OR cloudevent->>'type' ILIKE ANY(_type) )
	AND (_after = '' OR sequenceno >(
		SELECT
			case count(*)
			when 0
				then 0
			else
				(SELECT sequenceno
				FROM events.events
				WHERE cloudevent->>'id' = _after
				ORDER BY sequenceno ASC
				LIMIT 1)
			end
		FROM events.events
		WHERE cloudevent->>'id' = _after))
  ORDER BY sequenceno
  limit _size;
END;
$BODY$;
