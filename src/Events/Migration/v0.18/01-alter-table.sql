 ALTER TABLE events.events
 RENAME TO events_app;
 
 
ALTER TABLE events.events_app
RENAME CONSTRAINT events_pkey TO events_app_pkey;

