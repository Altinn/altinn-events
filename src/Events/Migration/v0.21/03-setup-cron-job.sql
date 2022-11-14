SELECT cron.schedule('0 3 * * *', $$DELETE FROM events.events WHERE registeredtime < now() - interval '90 days'$$);
