CREATE OR REPLACE FUNCTION events.find_subscription(
    _sourcefilter character varying,
    _subjectfilter character varying,
    _typefilter character varying,
    _consumer character varying,
    _endpointurl character varying
)
RETURNS TABLE(
    id bigint, 
    sourcefilter character varying, 
    subjectfilter character varying, 
    typefilter character varying, 
    consumer character varying, 
    endpointurl character varying, 
    createdby character varying, 
    validated boolean, 
    "time" timestamp with time zone
) 
    LANGUAGE 'plpgsql'
    COST 100
    VOLATILE PARALLEL UNSAFE
    ROWS 1000

AS $BODY$

BEGIN
RETURN query 
    SELECT 
        s.id, s.sourcefilter, s.subjectfilter, s.typefilter, s.consumer, s.endpointurl, s.createdby, s.validated, s."time"
    FROM 
        events.subscription s
    WHERE 
        s.sourcefilter = _sourcefilter
        AND (_subjectfilter IS NULL OR s.subjectfilter = _subjectfilter)
        AND (_typefilter IS NULL OR s.typefilter = _typefilter)
        AND s.consumer = _consumer
        AND s.endpointurl = _endpointurl;

END
$BODY$;