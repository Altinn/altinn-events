CREATE OR REPLACE PROCEDURE events.insertevent(
	id character varying,
	source character varying,
	subject character varying,
	type character varying,
	"time" timestamptz,
	cloudevent text)
    LANGUAGE 'plpgsql'
    
AS $BODY$

  BEGIN
	INSERT INTO events.events(id, source, subject, type, "time", cloudevent)
	  VALUES ($1, $2, $3, $4, $5, $6);
  END;

$BODY$;
