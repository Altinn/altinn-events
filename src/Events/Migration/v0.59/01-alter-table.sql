ALTER TABLE events.events ADD COLUMN IF NOT EXISTS status smallint NULL;
ALTER TABLE events.events ALTER COLUMN status SET DEFAULT 1;

ALTER TABLE events.events ADD COLUMN IF NOT EXISTS retrycount int NULL DEFAULT 0;
ALTER TABLE events.events ADD COLUMN IF NOT EXISTS retryReason text NULL;
ALTER TABLE events.events ADD COLUMN IF NOT EXISTS lastretried timestamptz NULL;

ALTER TABLE events.events DROP CONSTRAINT IF EXISTS events_status_fkey;
ALTER TABLE events.events
    ADD CONSTRAINT events_status_fkey
    FOREIGN KEY (status)
    REFERENCES events.event_status (id);

CREATE INDEX IF NOT EXISTS idx_events_status_sequenceno
    ON events.events (status, sequenceno);