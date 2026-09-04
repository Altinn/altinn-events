ALTER TABLE events.events ADD COLUMN IF NOT EXISTS idempotencyid uuid NULL;

CREATE UNIQUE INDEX IF NOT EXISTS events_idempotencyid_idx
    ON events.events (idempotencyid)
    WHERE idempotencyid IS NOT NULL;
