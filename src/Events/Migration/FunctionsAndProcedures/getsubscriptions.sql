CREATE OR REPLACE FUNCTION events.getsubscriptions_v2(
	resource character varying,
	subject character varying,
	type character varying)
	RETURNS TABLE(id bigint, resourcefilter character varying, sourcefilter character varying, subjectfilter character varying, typefilter character varying, consumer character varying, endpointurl character varying, createdby character varying, validated boolean, "time" timestamp with time zone, includesubunits boolean)
	LANGUAGE 'plpgsql'

AS $BODY$
BEGIN
return query
	SELECT s.id, s.resourcefilter, s.sourcefilter, s.subjectfilter, s.typefilter, s.consumer, s.endpointurl, s.createdby, s.validated, s."time", s.includesubunits
	FROM events.subscription s
	WHERE  s.resourcefilter = resource
		AND (s.subjectfilter is NULL OR s.subjectfilter = subject)
		AND (s.typefilter is NULL OR s.typefilter = type)
		AND s.validated;

END;
$BODY$;
