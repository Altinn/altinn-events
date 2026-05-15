DROP FUNCTION IF EXISTS events.find_subscription(character varying, character varying, character varying, character varying, character varying, character varying);

DROP FUNCTION IF EXISTS events.getsubscription_v2(integer);

DROP FUNCTION IF EXISTS events.getevents(character varying, character varying, text[], text[], integer);

DROP FUNCTION IF EXISTS events.getsubscriptionsbyconsumer_v2(character varying, boolean);
