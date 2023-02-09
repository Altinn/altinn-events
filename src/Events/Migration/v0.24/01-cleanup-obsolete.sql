drop function if exists events.getsubscriptions(source character varying, subject character varying, type character varying);

drop function if exists events.insert_subscription(sourcefilter character varying, subjectfilter character varying, typefilter character varying, consumer character varying, endpointurl character varying, createdby character varying, validated boolean);
