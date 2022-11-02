 ALTER TABLE events.events
 RENAME TO events_app;

ALTER TABLE events.events_app
RENAME CONSTRAINT events_pkey TO events_app_pkey;

ALTER  FUNCTION events.insertevent(
	id character varying,
	source character varying,
	subject character varying,
	type character varying,
	INOUT cloudevent text)
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