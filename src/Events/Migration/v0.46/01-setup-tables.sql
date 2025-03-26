DROP TABLE IF EXISTS events.trace_log;

CREATE TABLE IF NOT EXISTS events.trace_log
(
    cloudeventid uuid NOT NULL,
    resource text COLLATE pg_catalog."default",
    eventtype text COLLATE pg_catalog."default",
    consumer text COLLATE pg_catalog."default",
    "time" timestamp with time zone NOT NULL DEFAULT (now() AT TIME ZONE 'utc'::text),
    subscriptionid bigint,
    responsecode integer,
    subscriberendpoint text COLLATE pg_catalog."default",
    activity text COLLATE pg_catalog."default",
    sequenceno BIGSERIAL NOT NULL,
    CONSTRAINT trace_log_pkey PRIMARY KEY (sequenceno, "time")
) PARTITION BY RANGE ("time");

CREATE INDEX ON events.trace_log (time);
