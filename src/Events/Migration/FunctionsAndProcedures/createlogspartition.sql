CREATE OR REPLACE FUNCTION events.create_logs_partition(
	)
    RETURNS integer
    LANGUAGE 'plpgsql'
    COST 100
    VOLATILE PARALLEL UNSAFE
AS $BODY$
DECLARE
   thisMonth int; 
   nextMonth int;
   thisYear int;
   nextYear int;
   fromMonth varchar;
   toMonth varchar;
   partitionName varchar;
BEGIN
   SELECT (EXTRACT(MONTH from current_date)) INTO thisMonth;
   SELECT (EXTRACT(YEAR from current_date)) INTO thisYear;

nextYear = thisYear;
   nextMonth = thisMonth + 1;

   IF nextMonth = 13 THEN
      nextMonth = 1;
	  nextYear = nextYear + 1;
  END IF;

   fromMonth = thisYear || '-' || lpad(thisMonth::varchar, 2, '0') || '-01' ; 
   toMonth = nextYear || '-' || lpad(nextMonth::varchar, 2, '0') || '-01';
   partitionName = 'events.trace_log_y' || nextYear || 'm' || nextMonth;	
   
   EXECUTE
      format('CREATE TABLE %s PARTITION OF events.trace_log FOR VALUES FROM (''%s'') TO (''%s'')', partitionName, fromMonth, toMonth);
   
   RETURN 1;
END;
$BODY$;
