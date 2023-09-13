CREATE INDEX IF NOT EXISTS idx_events_cloudevent_resource_sequenceno
ON events.events ((cloudevent ->> 'resource'), sequenceno);
