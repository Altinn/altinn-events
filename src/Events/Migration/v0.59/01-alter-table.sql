ALTER TABLE events.events ADD COLUMN IF NOT EXISTS status smallint NULL;
-- No default: rows inserted by pods still running the previous version (which does not set status)
-- must stay NULL, otherwise they would be claimed and processed a second time after the deploy.
ALTER TABLE events.events ALTER COLUMN status DROP DEFAULT;

ALTER TABLE events.events ADD COLUMN IF NOT EXISTS retrycount int NULL DEFAULT 0;
ALTER TABLE events.events ADD COLUMN IF NOT EXISTS retryReason text NULL;
ALTER TABLE events.events ADD COLUMN IF NOT EXISTS lastretried timestamptz NULL;

ALTER TABLE events.events DROP CONSTRAINT IF EXISTS events_status_fkey;
ALTER TABLE events.events
    ADD CONSTRAINT events_status_fkey
    FOREIGN KEY (status)
    REFERENCES events.event_status (id);

CREATE INDEX IF NOT EXISTS idx_events_sequenceno_filtered
    ON events.events USING btree
    (sequenceno ASC NULLS LAST)
    WHERE status = 1;
