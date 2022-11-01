DROP FUNCTION IF EXISTS events.insertappevent(character varying, character varying, character varying, character varying, timestamptz, text);

CREATE OR REPLACE PROCEDURE events.insertappevent(
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
