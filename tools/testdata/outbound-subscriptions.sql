-- These units (partyid and organization numners) exists in the AT23 environment and can be used for LOCAL testing with port-forwarding against Register in AT23.
-- The creator and consumer on each subscription below are random. The subscriptions were created to test the subscription matching. Not the consumer authorization.

-- Main unit { party id: 51326198, organization number: 314249879 }
-- Sub unit { party id: 51326197, organization number: 311443755 }

-- insert_subscription_v2(resourcefilter, sourcefilter, subjectfilter, typefilter, consumer, endpoint address, createdby, validated, includesubunits);

-- Subunit subscriptions should only get subunit events.
SELECT events.insert_subscription_v2('urn:altinn:resource:app_ttd_apps-test', NULL, '/party/51326197',                                 NULL, '/org/nav',    'https://webhook.example.com/nav',          '/org/nav',    true,  false);
SELECT events.insert_subscription_v2('urn:altinn:resource:app_ttd_apps-test', NULL, '/party/51326197',                                 NULL, '/org/nav',    'https://webhook.example.com/nav',          '/org/nav',    true,  true);
-- Main unit subscription that should also get subunit events.
SELECT events.insert_subscription_v2('urn:altinn:resource:app_ttd_apps-test', NULL, '/party/51326198',                                 NULL, '/org/skd',    'https://webhook.example.com/skd',          '/org/skd',    true,  true);
-- Main unit that should only get main unit events.
SELECT events.insert_subscription_v2('urn:altinn:resource:app_ttd_apps-test', NULL, '/party/51326198',                                 NULL, '/org/skd',    'https://webhook.example.com/skd',          '/org/skd',    true,  false);

-- Subunit subscriptions should only get subunit events.
SELECT events.insert_subscription_v2('urn:altinn:resource:nabovarsel',        NULL, 'urn:altinn:organization:identifier-no:311443755', NULL, '/org/digdir', 'https://webhook.example.com/digdir-sub',   '/org/digdir', true,  false);
SELECT events.insert_subscription_v2('urn:altinn:resource:nabovarsel',        NULL, 'urn:altinn:organization:identifier-no:311443755', NULL, '/org/digdir', 'https://webhook.example.com/digdir-sub',   '/org/digdir', true,  true);
-- Main unit subscription that should also get subunit events.
SELECT events.insert_subscription_v2('urn:altinn:resource:nabovarsel',        NULL, 'urn:altinn:organization:identifier-no:314249879', NULL, '/org/ttd',    'https://webhook.example.com/ttd-main',     '/org/ttd',    true,  true);
-- Main unit that should only get main unit events.
SELECT events.insert_subscription_v2('urn:altinn:resource:nabovarsel',        NULL, 'urn:altinn:organization:identifier-no:314249879', NULL, '/org/ttd',    'https://webhook.example.com/ttd-main',     '/org/ttd',    true,  false);

-- A few extra filler subscriptions.
SELECT events.insert_subscription_v2('urn:altinn:resource:app_ttd_other-app',    NULL, '/party/52326192',                                 NULL,  '/org/nav',   'https://webhook.example.com/nav-other',       '/org/nav',   true,  false);
SELECT events.insert_subscription_v2('urn:altinn:resource:app_ttd_apps-test',    NULL, '/party/53326193',                                 NULL,  '/org/nav',   'https://webhook.example.com/nav-wrong-type',  '/org/nav',   true,  false);
SELECT events.insert_subscription_v2('urn:altinn:resource:nabovarsel',           NULL, 'urn:altinn:organization:identifier-no:324259572', NULL,  '/org/nav',   'https://webhook.example.com/nav-unvalidated', '/org/nav',   false, false);
SELECT events.insert_subscription_v2('urn:altinn:resource:some_other_resource',  NULL, 'urn:altinn:organization:identifier-no:313133745', NULL,  '/org/other', 'https://webhook.example.com/other',           '/org/other', true,  false);

-- Subscriptions for testing of source filtering.
SELECT events.insert_subscription_v2('urn:altinn:resource:app_ttd_apps-test',  'https://ttd.apps.at23.altinn.cloud/ttd/apps-test',                       NULL, NULL,  '/org/ttd', 'https://webhook.example.com/ttd', '/org/ttd', true,  false);
SELECT events.insert_subscription_v2('urn:altinn:resource:app_ttd_apps-test',  NULL,                                                                     NULL, NULL,  '/org/ttd', 'https://webhook.example.com/ttd', '/org/ttd', true,  false);
SELECT events.insert_subscription_v2('urn:altinn:resource:app_ttd_apps-test',  'https://platform.at23.altinn.cloud/dialogporten/api/v1/enduser/dialogs', NULL, NULL,  '/org/ttd', 'https://webhook.example.com/ttd', '/org/ttd', true,  false);

