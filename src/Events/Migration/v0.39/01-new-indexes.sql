CREATE INDEX IF NOT EXISTS idx_events_cloudevent_subject_time
    ON events.events ((cloudevent ->> 'subject'), (cloudevent ->> 'time'));
