--- Create B-tree indices on json columns
CREATE INDEX IF NOT EXISTS idx_events_cloudevent_id
    ON events.events ((cloudevent ->> 'id'));
CREATE INDEX IF NOT EXISTS idx_events_cloudevent_subject
    ON events.events ((cloudevent ->> 'subject'));
CREATE INDEX IF NOT EXISTS idx_events_cloudevent_alternativesubject
    ON events.events ((cloudevent ->> 'alternativesubject'));
CREATE INDEX IF NOT EXISTS idx_events_cloudevent_source
    ON events.events ((cloudevent ->> 'source'));
CREATE INDEX IF NOT EXISTS idx_events_cloudevent_type
    ON events.events ((cloudevent ->> 'type'));

-- Drop redundant GIN index
DROP INDEX IF EXISTS events.idx_gin_events_computed_cloudevent;
