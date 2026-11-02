-- Partial index to support monitoring of events in status RetryExhausted (3)
CREATE INDEX IF NOT EXISTS idx_events_registeredtime_filtered_retryexhausted
    ON events.events USING btree
    (registeredtime ASC NULLS LAST)
    WHERE status = 3;
