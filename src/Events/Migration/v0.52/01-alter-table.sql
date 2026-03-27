ALTER TABLE events.subscription
    ADD COLUMN IF NOT EXISTS includesubunits boolean NULL;
