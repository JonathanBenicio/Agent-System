-- Migration: Criar tabela de sessões para PostgresSessionStore
-- Flyway: V001__Create_sessions_table.sql

CREATE TABLE IF NOT EXISTS sessions (
    id              TEXT        PRIMARY KEY,
    user_id         TEXT        NOT NULL,
    tenant_id       TEXT        NOT NULL DEFAULT 'default',
    data            JSONB       NOT NULL,
    started_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    ended_at        TIMESTAMPTZ,
    is_consolidated BOOLEAN     NOT NULL DEFAULT FALSE
);

CREATE INDEX idx_sessions_user_id   ON sessions (user_id);
CREATE INDEX idx_sessions_tenant_id ON sessions (tenant_id);
CREATE INDEX idx_sessions_started   ON sessions (started_at DESC);
