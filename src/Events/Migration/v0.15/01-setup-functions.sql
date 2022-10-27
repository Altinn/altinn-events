CREATE OR REPLACE FUNCTION events.insertevent(
	id character varying,
	source character varying,
	subject character varying,
	type character varying,
	time timestamptz,
	cloudevent INOUT text)
    LANGUAGE 'plpgsql'
    
AS $BODY$
	DECLARE storedCloudEvent text;

  BEGIN
	INSERT INTO events.events(id, source, subject, type, "time", cloudevent)
	  VALUES ($1, $2, $3, $4, $5, $6);

	SELECT storedCloudEvent
	INTO cloudevent;
  END;

$BODY$;
