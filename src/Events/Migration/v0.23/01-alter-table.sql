ALTER TABLE events.subscription
ADD sourcefilterhash character varying; 

CREATE INDEX IF NOT EXISTS idx_btree_subscription_sourcefilterhash ON events.subscription USING btree (sourcefilterhash);

UPDATE events.subscription
	SET sourcefilterhash=Upper(MD5(sourcefilter));
