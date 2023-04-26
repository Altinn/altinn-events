
CREATE OR REPLACE FUNCTION events.insert_subscription(
    resourcefilter character varying,
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
  INSERT INTO events.subscription(resourcefilter, sourcefilter, subjectfilter, typefilter, consumer, endpointurl, createdby, "time", validated, sourcefilterhash)
  VALUES ($1, $2, $3, $4, $5, $6, $7,  currentTime, $8, $9) RETURNING *;

END
$BODY$;


CREATE OR REPLACE FUNCTION events.find_subscription(
    _resourcefilter character varying,
	_sourcefilter character varying,
	_subjectfilter character varying,
	_typefilter character varying,
	_consumer character varying,
	_endpointurl character varying)
    RETURNS TABLE(id bigint, resourcefilter character varying, sourcefilter character varying, subjectfilter character varying, typefilter character varying, consumer character varying, endpointurl character varying, createdby character varying, validated boolean, "time" timestamp with time zone)
    LANGUAGE 'plpgsql'
    COST 100
    VOLATILE PARALLEL UNSAFE
    ROWS 1000

AS $BODY$


BEGIN
RETURN query
    SELECT
        s.id, s.resourcefilter, s.sourcefilter, s.subjectfilter, s.typefilter, s.consumer, s.endpointurl, s.createdby, s.validated, s."time"
    FROM
        events.subscription s
    WHERE
        s.resourcefilter = _resourcefilter
        AND s.sourcefilter = _sourcefilter
        AND (_subjectfilter IS NULL OR s.subjectfilter = _subjectfilter)
        AND (_typefilter IS NULL OR s.typefilter = _typefilter)
        AND s.consumer = _consumer
        AND s.endpointurl = _endpointurl;

END
$BODY$;

CREATE OR REPLACE FUNCTION events.getsubscription(
	_id integer)
    RETURNS TABLE(id bigint, sourcefilter character varying, subjectfilter character varying, typefilter character varying, consumer character varying, endpointurl character varying, createdby character varying, validated boolean, "time" timestamp with time zone)
    LANGUAGE 'plpgsql'
AS $BODY$

BEGIN
return query
	SELECT s.id, s.resourcefilter, s.sourcefilter, s.subjectfilter, s.typefilter, s.consumer, s.endpointurl, s.createdby, s.validated, s."time"
	FROM events.subscription s
  where s.id = _id;

END;
$BODY$;



CREATE OR REPLACE FUNCTION events.getsubscriptions_v2(
	sourcehashset character varying[],
	subject character varying,
	type character varying)
    RETURNS TABLE(id bigint, resourcefilter character varying, sourcefilter character varying, subjectfilter character varying, typefilter character varying, consumer character varying, endpointurl character varying, createdby character varying, validated boolean, "time" timestamp with time zone)
    LANGUAGE 'plpgsql'
AS $BODY$

BEGIN
return query
	SELECT s.id, s.resourcefilter, s.sourcefilter, s.subjectfilter, s.typefilter, s.consumer, s.endpointurl, s.createdby, s.validated, s."time"
	FROM events.subscription s
  WHERE  s.sourcefilterhash = ANY(sourcehashset)
  AND (s.subjectfilter is NULL OR s.subjectfilter = subject)
  AND (s.typefilter is NULL OR s.typefilter = type)
  AND s.validated;

END;
$BODY$;


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
