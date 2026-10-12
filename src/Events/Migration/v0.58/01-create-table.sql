-- Lookup table for events.events.status
CREATE TABLE IF NOT EXISTS events.event_status
(
    id smallint NOT NULL PRIMARY KEY,
    name text NOT NULL UNIQUE
);

INSERT INTO events.event_status (id, name)
VALUES
    (1, 'Registered'),
    (2, 'Processed'),
    (3, 'RetryExhausted')
ON CONFLICT (id) DO NOTHING;

GRANT SELECT ON events.event_status TO platform_events;