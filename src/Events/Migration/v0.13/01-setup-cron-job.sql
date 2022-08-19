CREATE EXTENSION IF NOT EXISTS pg_cron;

SELECT cron.schedule('0 3 * * *', $$DELETE FROM events.events WHERE time < now() - interval '90 days'$$);
