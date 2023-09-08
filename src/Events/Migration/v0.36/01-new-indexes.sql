CREATE INDEX IF NOT EXISTS idx_events_cloudevent_resource
    ON events.events ((cloudevent ->> 'resource'));
