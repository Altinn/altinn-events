-- This script is autogenerated from the tool DbTools. Do not edit manually.

-- deletesubscription.sql:
CREATE OR REPLACE PROCEDURE events.deletesubscription(_id integer)
    LANGUAGE 'plpgsql'
    
AS $BODY$
BEGIN
  DELETE 
	FROM events.subscription s
  where s.id = _id;

END;
$BODY$;

-- findsubscription.sql:
CREATE OR REPLACE FUNCTION events.find_subscription(
    _resourcefilter character varying,
	_sourcefilter character varying,
	_subjectfilter character varying,
	_typefilter character varying,
	_consumer character varying,
	_endpointurl character varying)
    RETURNS TABLE(id bigint, resourcefilter character varying, sourcefilter character varying, subjectfilter character varying, typefilter character varying, consumer character varying, endpointurl character varying, createdby character varying, validated boolean, "time" timestamp with time zone)
    LANGUAGE 'plpgsql'
AS $BODY$

BEGIN
RETURN query
    SELECT
        s.id, s.resourcefilter, s.sourcefilter, s.subjectfilter, s.typefilter, s.consumer, s.endpointurl, s.createdby, s.validated, s."time"
    FROM
        events.subscription s
    WHERE
        s.resourcefilter = _resourcefilter
        AND (s.sourcefilter IS NULL OR s.sourcefilter = _sourcefilter)
        AND (_subjectfilter IS NULL OR s.subjectfilter = _subjectfilter)
        AND ((_typefilter IS NULL AND s.typefilter IS NULL) OR s.typefilter = _typefilter)
        AND s.consumer = _consumer
        AND s.endpointurl = _endpointurl;
END
$BODY$;

-- getappevents.sql:
CREATE OR REPLACE FUNCTION events.getappevents_v2(
	_subject character varying,
	_after character varying,
	_from timestamp with time zone,
	_to timestamp with time zone,
	_type text[],
	_source text[],
	_resource text,
	_size integer)
    RETURNS TABLE(cloudevents text)
    LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
_sequenceno_first bigint;
_sequenceno_last bigint;
BEGIN
IF _after IS NOT NULL AND _after <> '' THEN
	SELECT
		case count(*)
		when 0
			then 0
		else
			(SELECT MIN(sequenceno)
			FROM events.events
			WHERE cloudevent->>'id' = _after)
		end
	INTO _sequenceno_first
	FROM events.events
	WHERE cloudevent->>'id' = _after;
END IF;
SELECT MAX(sequenceno) INTO _sequenceno_last FROM events.events WHERE registeredtime <= now() - interval '30 second';
return query
	SELECT cast(cloudevent as text) as cloudevents
		FROM events.events
		WHERE (_subject IS NULL OR cloudevent->>'subject' = _subject)
			AND (_from IS NULL OR cloudevent->>'time' >= _from::text)
			AND (_to IS NULL OR cloudevent->>'time' <= _to::text)
			AND (_type IS NULL OR cloudevent->>'type' ILIKE ANY(_type))
			AND (_source IS NULL OR cloudevent->>'source' ILIKE ANY(_source))
			AND (_resource IS NULL OR cloudevent->>'resource' = _resource)
			AND (_after IS NULL OR _after = '' OR sequenceno > _sequenceno_first)
			AND (_sequenceno_last IS NULL OR sequenceno <= _sequenceno_last)
		ORDER BY sequenceno
		limit _size;
END;
$BODY$;


-- getevents.sql:
CREATE OR REPLACE FUNCTION events.getevents(
	_resource character varying,
	_subject character varying,
	_alternativesubject character varying,
	_after character varying,
	_type text[],
	_size integer)
    RETURNS TABLE(cloudevents text) 
    LANGUAGE 'plpgsql'
AS $BODY$

DECLARE
_sequenceno bigint;
BEGIN
IF _after IS NOT NULL AND _after <> '' THEN
	SELECT
		case count(*)
		when 0
			then 0
		else
			(SELECT MIN(sequenceno) FROM events.events
			WHERE cloudevent->>'id' = _after)
		end
	INTO _sequenceno
	FROM events.events
	WHERE cloudevent->>'id' = _after;
END IF;
return query
	SELECT cast(cloudevent as text) as cloudevents
	FROM events.events
	WHERE  cloudevent->>'resource' = _resource
	AND (_subject IS NULL OR cloudevent->>'subject' = _subject)
	AND (_alternativeSubject IS NULL OR cloudevent->>'alternativesubject' = _alternativesubject)
	AND (_type IS NULL OR cloudevent->>'type' LIKE ANY(_type) )
	AND registeredtime <= now() - interval '30 second'
	AND (_after IS NULL OR _after = '' OR sequenceno > _sequenceno)
  ORDER BY sequenceno
  limit _size;
END;
$BODY$;

-- getsubscription.sql:
CREATE OR REPLACE FUNCTION events.getsubscription_v2(
	_id integer)
    RETURNS TABLE(id bigint, resourcefilter character varying, sourcefilter character varying, subjectfilter character varying, typefilter character varying, consumer character varying, endpointurl character varying, createdby character varying, validated boolean, "time" timestamp with time zone)
    LANGUAGE 'plpgsql'
AS $BODY$

BEGIN
return query
	SELECT s.id, s.resourcefilter, s.sourcefilter, s.subjectfilter, s.typefilter, s.consumer, s.endpointurl, s.createdby, s.validated, s."time"
	FROM events.subscription s
  where s.id = _id;

END;
$BODY$;

-- getsubscriptions.sql:
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

-- getsubscriptionsbyconsumer.sql:
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

-- insertsubscription.sql:
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

-- setvalidsubscription.sql:
CREATE OR REPLACE PROCEDURE events.setvalidsubscription(_id integer)
    LANGUAGE 'plpgsql'
    
AS $BODY$
BEGIN
  UPDATE 
	events.subscription
	  SET
   validated = true
	WHERE id = _id;
END;
$BODY$;


