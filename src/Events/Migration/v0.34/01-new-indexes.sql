CREATE INDEX IF NOT EXISTS idx_subscription_resourcefilter
    ON events.subscription ((resourcefilter));
