-- Migration: Criar tabelas para MigrationJobStore e EmbeddingModelStore
-- Flyway: V002__Create_migration_embedding_tables.sql

CREATE TABLE IF NOT EXISTS migration_jobs (
    id          TEXT        PRIMARY KEY,
    status      TEXT        NOT NULL DEFAULT 'Pending',
    data        JSONB       NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_migration_jobs_status     ON migration_jobs (status);
CREATE INDEX idx_migration_jobs_created    ON migration_jobs (created_at DESC);

CREATE TABLE IF NOT EXISTS embedding_models (
    id          TEXT        PRIMARY KEY,
    name        TEXT        NOT NULL,
    is_active   BOOLEAN     NOT NULL DEFAULT FALSE,
    data        JSONB       NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_embedding_models_active   ON embedding_models (is_active) WHERE is_active = true;
CREATE INDEX idx_embedding_models_name     ON embedding_models (name);
