drop function events.getsubscription(_id integer);


drop function events.getsubscriptions(sourcehashset character varying[], subject character varying, type character varying);


drop function events.getsubscriptionsbyconsumer(_consumer character varying, _includeinvalid boolean);

drop function events.find_subscription(_sourcefilter character varying, _subjectfilter character varying, _typefilter character varying, _consumer character varying, _endpointurl character varying);

 drop function events.insert_subscription(sourcefilter character varying, subjectfilter character varying, typefilter character varying, consumer character varying, endpointurl character varying, createdby character varying, validated boolean, sourcefilterhash character varying);