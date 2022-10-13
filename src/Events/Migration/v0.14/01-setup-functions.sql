CREATE OR REPLACE FUNCTION events.getsubscriptions(
	source character varying,
	subject character varying,
	type character varying)
    RETURNS TABLE(
		id bigint,
		sourcefilter character varying, 
		subjectfilter character varying, 
		typefilter character varying, 
		consumer character varying, 
		endpointurl character varying, 
		createdby character varying, 
		validated boolean, 
		"time" timestamp with time zone) 
    LANGUAGE 'plpgsql'
    COST 100
    VOLATILE PARALLEL UNSAFE
    ROWS 1000

AS $BODY$

BEGIN
return query 
	SELECT s.id, s.sourcefilter, s.subjectfilter, s.typefilter, s.consumer, s.endpointurl, s.createdby, s.validated, s."time"
	FROM events.subscription s
  WHERE (s.subjectfilter is NULL OR s.subjectfilter = subject)
  AND position(s.sourcefilter in source) = 1
  AND (s.typefilter is NULL OR s.typefilter = type)
  AND s.validated = true;

END;
$BODY$;
