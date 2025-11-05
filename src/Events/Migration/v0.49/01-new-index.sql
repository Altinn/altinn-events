CREATE INDEX IF NOT EXISTS idx_events_cloudevent_resource_time
    ON events.events ((cloudevent ->> 'resource'), (cloudevent ->> 'time'));  