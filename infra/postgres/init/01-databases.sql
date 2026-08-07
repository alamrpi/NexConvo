-- Database-per-service (skill Standard 5). Each microservice owns a private database;
-- no service connects to another's. RLS inside each database handles tenancy (Standard 6).
CREATE DATABASE nexconvo_identity;
CREATE DATABASE nexconvo_crm;
CREATE DATABASE nexconvo_chat;
CREATE DATABASE nexconvo_voice;
CREATE DATABASE nexconvo_automation;
CREATE DATABASE nexconvo_ai;
-- NOTE: init scripts only run on FIRST postgres volume init. On existing dev volumes the
-- Knowledge service's privileged migrator connection creates this database automatically
-- via EF MigrateAsync (same as nexconvo_integrations today).
CREATE DATABASE nexconvo_knowledge;

-- Non-superuser login the services connect with, so RLS is actually enforced (a superuser
-- bypasses RLS). LOCAL-DEV credential only — production uses managed secrets (Standard 13).
CREATE ROLE nexconvo_service WITH LOGIN PASSWORD 'localdev_service_pw';

GRANT CONNECT ON DATABASE
    nexconvo_identity, nexconvo_crm, nexconvo_chat,
    nexconvo_voice, nexconvo_automation, nexconvo_ai,
    nexconvo_knowledge
    TO nexconvo_service;

-- Per-table RLS policies + the current_tenant_id() helper are added by each service's
-- EF Core migrations against its own database — not here.
