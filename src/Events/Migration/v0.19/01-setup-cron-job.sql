DO
$do$
BEGIN
	IF EXISTS (SELECT setting FROM pg_settings WHERE name = 'azure.customer_resource_group') THEN
		SELECT cron.schedule('0 3 * * *', $$DELETE FROM events.events_app WHERE time < now() - interval '90 days'$$);
	END IF;
END
$do$