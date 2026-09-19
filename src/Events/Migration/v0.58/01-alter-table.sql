ALTER TABLE events.events ADD COLUMN IF NOT EXISTS status text NOT NULL DEFAULT 'registered';
ALTER TABLE events.events ADD COLUMN IF NOT EXISTS retrycount int NOT NULL DEFAULT 0;
ALTER TABLE events.events ADD COLUMN IF NOT EXISTS lastretried timestamptz NULL;

ALTER TABLE events.events DROP CONSTRAINT IF EXISTS events_status_check;
ALTER TABLE events.events ADD CONSTRAINT events_status_check
    CHECK (status IN ('registered', 'processed', 'retryExhausted'));

CREATE INDEX IF NOT EXISTS events_status_sequenceno_idx
    ON events.events (status, sequenceno);