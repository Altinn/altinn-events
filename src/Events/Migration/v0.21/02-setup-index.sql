CREATE INDEX IF NOT EXISTS idx_gin_events_computed_cloudevent ON events.events USING GIN (cloudevent jsonb_path_ops);

CREATE INDEX IF NOT EXISTS idx_events_computed_time ON events.events USING btree ("registeredtime" ASC NULLS LAST);
