 ALTER TABLE events.events
 RENAME TO events_app;

ALTER TABLE events.events_app
RENAME CONSTRAINT events_pkey TO events_app_pkey;

ALTER PROCEDURE events.insertevent(
	IN id character varying,
	IN source character varying,
	IN subject character varying,
	IN type character varying,
	IN "time" timestamp with time zone,
	IN cloudevent text)
RENAME TO insertappevent;

ALTER FUNCTION events.get(
	_subject character varying,
	_after character varying,
	_from timestamp with time zone,
	_to timestamp with time zone,
	_type text[],
	_source text[],
	_size integer)
RENAME TO getappevent;