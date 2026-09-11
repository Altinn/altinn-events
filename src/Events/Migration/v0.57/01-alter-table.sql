ALTER TABLE events.events ADD COLUMN IF NOT EXISTS idempotencykey uuid NULL;

CREATE UNIQUE INDEX IF NOT EXISTS events_idempotencykey_idx
    ON events.events (idempotencykey)
    WHERE idempotencykey IS NOT NULL;
