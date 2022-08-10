CREATE INDEX IF NOT EXISTS idx_events_id ON events.events USING btree (id ASC NULLS LAST);
