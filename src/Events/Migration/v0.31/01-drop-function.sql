drop function events.getsubscription(_id integer);


drop function events.getsubscriptions(sourcehashset character varying[], subject character varying, type character varying);


drop function events.insert_subscription(sourcefilter character varying, subjectfilter character varying, typefilter character varying, consumer character varying, endpointurl character varying, createdby character varying, validated boolean, sourcefilterhash character varying);
