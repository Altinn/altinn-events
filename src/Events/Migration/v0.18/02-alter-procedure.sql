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
	INSERT INTO events.events_app(id, source, subject, type, "time", cloudevent)
	  VALUES ($1, $2, $3, $4, $5, $6);
  END;

$BODY$;

 
ALTER PROCEDURE events.insertevent(
	IN id character varying,
	IN source character varying,
	IN subject character varying,
	IN type character varying,
	IN "time" timestamp with time zone,
	IN cloudevent text)
RENAME TO insertappevent;
 
