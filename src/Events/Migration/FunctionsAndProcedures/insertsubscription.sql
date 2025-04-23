CREATE OR REPLACE FUNCTION events.insert_subscription(
    resourcefilter character varying,
	sourcefilter character varying,
	subjectfilter character varying,
	typefilter character varying,
	consumer character varying,
	endpointurl character varying,
	createdby character varying,
	validated boolean)
	RETURNS SETOF events.subscription
	LANGUAGE 'plpgsql'

AS $BODY$
DECLARE currentTime timestamptz;

BEGIN
	SET TIME ZONE UTC;
	currentTime := NOW();

	RETURN QUERY
	INSERT INTO events.subscription(resourcefilter, sourcefilter, subjectfilter, typefilter, consumer, endpointurl, createdby, "time", validated)
	VALUES ($1, $2, $3, $4, $5, $6, $7,  currentTime, $8) RETURNING *;

END
$BODY$;
