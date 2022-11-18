DROP FUNCTION IF EXISTS events.get(character varying, character varying, timestamp with time zone, timestamp with time zone, text[], text[]);

DROP FUNCTION IF EXISTS events.get(character varying, character varying, timestamp with time zone, timestamp with time zone, text[], text[], integer);

DROP FUNCTION IF EXISTS events.getsubscriptionsbyconsumer(character varying);

DROP PROCEDURE IF EXISTS events.insert_event(character varying, character varying, character varying, character varying, text);

DROP FUNCTION IF EXISTS events.insertevent(character varying, character varying, character varying, character varying, text);
