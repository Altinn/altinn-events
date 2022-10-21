alter function events.getsubscriptionsbyconsumer(_consumer character varying) owner to platform_events_admin;


drop function if exists events.getsubscriptionsbyconsumer(_consumer character varying);
	