CREATE TABLE IF NOT EXISTS events.events (
    sequenceno BIGSERIAL,
    cloudevent JSONB NOT NULL,
    registeredtime timestamptz default (now() at time zone 'utc'),
    CONSTRAINT events_pkey PRIMARY KEY (sequenceno)
);