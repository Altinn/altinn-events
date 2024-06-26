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