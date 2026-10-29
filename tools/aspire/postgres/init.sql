-- Runs once, when the container's data volume is new. The admin user and its password come from the app host.
CREATE ROLE platform_events WITH LOGIN PASSWORD 'Password';
CREATE DATABASE eventsdb OWNER platform_events_admin;
