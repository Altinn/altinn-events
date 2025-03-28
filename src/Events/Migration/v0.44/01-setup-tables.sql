-- Table: events.trace_log

CREATE TABLE IF NOT EXISTS events.trace_log
(
    cloudeventid uuid NOT NULL,
    resource character varying COLLATE pg_catalog."default",
    eventtype character varying COLLATE pg_catalog."default",
    consumer character varying COLLATE pg_catalog."default",
    "time" timestamp with time zone DEFAULT (now() AT TIME ZONE 'utc'::text),
    subscriptionid bigint,
    responsecode integer,
    subscriberendpoint character varying COLLATE pg_catalog."default",
    activity character varying COLLATE pg_catalog."default",
    sequenceno BIGSERIAL,
    CONSTRAINT trace_log_pkey PRIMARY KEY (sequenceno)
)

TABLESPACE pg_default;
