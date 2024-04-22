CREATE OR REPLACE FUNCTION events.getsubscriptionsbyconsumer_v2(
	_consumer character varying,
	_includeinvalid boolean)
    RETURNS TABLE(id bigint, resourcefilter character varying, sourcefilter character varying, subjectfilter character varying, typefilter character varying, consumer character varying, endpointurl character varying, createdby character varying, validated boolean, "time" timestamp with time zone)
    LANGUAGE 'plpgsql'
AS $BODY$


BEGIN
return query
	SELECT s.id, s.resourcefilter, s.sourcefilter, s.subjectfilter, s.typefilter, s.consumer, s.endpointurl, s.createdby, s.validated, s."time"
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