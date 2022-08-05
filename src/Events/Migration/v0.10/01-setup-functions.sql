CREATE OR REPLACE FUNCTION events.getsubscriptionsbyconsumer(
	_consumer character varying,
	_includeInvalid boolean)
    RETURNS TABLE(id bigint, sourcefilter character varying, subjectfilter character varying, typefilter character varying, consumer character varying, endpointurl character varying, createdby character varying, validated boolean, "time" timestamp with time zone) 
    LANGUAGE 'plpgsql'
    COST 100
    VOLATILE PARALLEL UNSAFE
    ROWS 1000

AS $BODY$

BEGIN
return query 
	SELECT s.id, s.sourcefilter, s.subjectfilter, s.typefilter, s.consumer, s.endpointurl, s.createdby, s.validated, s."time"
	FROM events.subscription s
  WHERE s.consumer LIKE _consumer
  AND s.validated = 	
	  CASE WHEN _includeInvalid THEN 
		false or s.validated = true
	  ELSE
		true
	  END;


END
$BODY$;