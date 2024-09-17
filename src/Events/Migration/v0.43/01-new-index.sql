CREATE UNIQUE INDEX IF NOT EXISTS idx_events_id_source
    ON events.events ((cloudevent -> 'id'), (cloudevent -> 'source'));