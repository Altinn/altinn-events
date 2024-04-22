CREATE OR REPLACE PROCEDURE events.deletesubscription(_id integer)
    LANGUAGE 'plpgsql'
    
AS $BODY$
BEGIN
  DELETE 
	FROM events.subscription s
  where s.id = _id;

END;
$BODY$;