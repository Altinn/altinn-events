GRANT SELECT, INSERT, UPDATE, REFERENCES, DELETE, TRUNCATE, TRIGGER ON events.trace_log TO platform_events;

GRANT ALL ON events.trace_log_sequenceno_seq TO platform_events;
