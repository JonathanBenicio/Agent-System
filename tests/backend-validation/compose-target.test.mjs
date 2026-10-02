import assert from 'node:assert/strict';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';
import { assertApiTargetMatches, validationSql } from './compose-target.mjs';

test('API and diagnostic SQL must name the same isolated Compose project and database', t => {
  const output = mkdtempSync(join(tmpdir(), 'pr152-compose-target-'));
  const previousProject = process.env.BACKEND_VALIDATION_COMPOSE_PROJECT;
  const previousDatabase = process.env.BACKEND_VALIDATION_DATABASE;
  const previousOutput = process.env.BACKEND_VALIDATION_OUTPUT_DIR;
  t.after(() => {
    rmSync(output, { recursive: true, force: true });
    if (previousProject === undefined) delete process.env.BACKEND_VALIDATION_COMPOSE_PROJECT;
    else process.env.BACKEND_VALIDATION_COMPOSE_PROJECT = previousProject;
    if (previousDatabase === undefined) delete process.env.BACKEND_VALIDATION_DATABASE;
    else process.env.BACKEND_VALIDATION_DATABASE = previousDatabase;
    if (previousOutput === undefined) delete process.env.BACKEND_VALIDATION_OUTPUT_DIR;
    else process.env.BACKEND_VALIDATION_OUTPUT_DIR = previousOutput;
  });

  process.env.BACKEND_VALIDATION_COMPOSE_PROJECT = 'agent-system-pr152-test';
  process.env.BACKEND_VALIDATION_DATABASE = 'review_pr152_test';
  process.env.BACKEND_VALIDATION_OUTPUT_DIR = output;
  writeFileSync(join(output, 'api-target.json'), JSON.stringify({
    composeProject: 'agent-system-pr152-test',
    database: 'review_pr152_test',
    host: '127.0.0.1',
    port: 55432,
  }));
  assert.doesNotThrow(() => assertApiTargetMatches(output));

  process.env.BACKEND_VALIDATION_DATABASE = 'review_pr152_another_run';
  assert.throws(() => assertApiTargetMatches(output), /targets do not match/);
  process.env.BACKEND_VALIDATION_DATABASE = 'backend_validation';
  assert.throws(() => validationSql('SELECT 1;'), /exclusive review_pr152_/);
});
