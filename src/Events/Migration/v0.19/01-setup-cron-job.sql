SELECT cron.schedule('0 3 * * *', $$DELETE FROM events.events_app WHERE time < now() - interval '90 days'$$);
