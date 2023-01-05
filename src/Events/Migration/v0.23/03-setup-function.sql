CREATE OR REPLACE FUNCTION events.insert_subscription(
    sourcefilter character varying,
    subjectfilter character varying,
    typefilter character varying,
    consumer character varying,
    endpointurl character varying,
    createdby character varying,
    validated boolean,
    sourcefilterhash character varying)
  RETURNS SETOF events.subscription 
LANGUAGE 'plpgsql'
AS $BODY$

DECLARE currentTime timestamptz; 

BEGIN
  SET TIME ZONE UTC;
  currentTime := NOW();

  RETURN QUERY
  INSERT INTO events.subscription(sourcefilter, subjectfilter, typefilter, consumer, endpointurl, createdby, "time", validated, sourcefilterhash)
  VALUES ($1, $2, $3, $4, $5, $6, currentTime, $7, $8) RETURNING *;

END
$BODY$;




CREATE OR REPLACE FUNCTION events.getsubscriptions(
	sourcehashset character varying[],
	subject character varying,
	type character varying)
    RETURNS TABLE(id bigint, sourcefilter character varying, subjectfilter character varying, typefilter character varying, consumer character varying, endpointurl character varying, createdby character varying, validated boolean, "time" timestamp with time zone) 
LANGUAGE 'plpgsql'
AS $BODY$

BEGIN
return query 
	SELECT s.id, s.sourcefilter, s.subjectfilter, s.typefilter, s.consumer, s.endpointurl, s.createdby, s.validated, s."time"
	FROM events.subscription s
  WHERE  s.sourcefilterhash = ANY(sourcehashset)
  AND (s.subjectfilter is NULL OR s.subjectfilter = subject)
  AND (s.typefilter is NULL OR s.typefilter = type)
  AND s.validated;

END;
$BODY$;
